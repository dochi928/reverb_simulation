import os
import sys
import numpy as np
from scipy.io import wavfile
from scipy.signal import fftconvolve
import scipy.interpolate as interp

import tkinter as tk
from tkinter import filedialog, messagebox

def select_file_via_gui(title_text, file_types):
    root = tk.Tk()
    root.withdraw()
    root.attributes("-topmost", True)
    file_path = filedialog.askopenfilename(title=title_text, filetypes=file_types)
    root.destroy()
    return file_path

def select_save_path_via_gui(title_text, default_name, file_types):
    root = tk.Tk()
    root.withdraw()
    root.attributes("-topmost", True)
    file_path = filedialog.asksaveasfilename(title=title_text, initialfile=default_name, filetypes=file_types)
    root.destroy()
    return file_path

def apply_high_res_interference_reverb(input_wav_path, simulation_log_path, output_wav_path, target_sr=44100):
    # 1. 입력 음원 로드
    sample_rate, data = wavfile.read(input_wav_path)
    if len(data.shape) == 1:
        input_left = input_right = data.astype(np.float32) / 32768.0
    else:
        input_left = data[:, 0].astype(np.float32) / 32768.0
        input_right = data[:, 1].astype(np.float32) / 32768.0

    src_time_res = 640000
    vol_res = 1000000

    direct_l_idx = 0
    direct_r_idx = 0

    bands_raw_timeline = {}
    current_freq = 100
    max_delay_idx = 0

    print("\n[1/6] 640kHz 초고해상도 타임라인 크기 분석 중...")
    with open(simulation_log_path, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line: continue
            if line.startswith("DIRECT_INFO_L"):
                direct_l_idx = int(line.split()[2])
                continue
            if line.startswith("DIRECT_INFO_R"):
                direct_r_idx = int(line.split()[2])
                continue
            if line.startswith("Time Resolution"):
                src_time_res = int(line.split(":")[-1].strip())
                continue
            if line.startswith("Volume Resolution"):
                vol_res = float(line.split(":")[-1].strip())
                continue

            parts = line.split()
            if len(parts) >= 4 and not line.endswith("Hz") and not line.startswith("MODE"):
                delay_idx = int(parts[1])
                if delay_idx > max_delay_idx:
                    max_delay_idx = delay_idx

    high_res_ir_length = max_delay_idx + 10
    print(f"-> 640kHz 기준 임펄스 배열 길이: {high_res_ir_length} 샘플 지정 완료.")

    print("[2/6] 640kHz 영역에서 이웃 프레임 간 보강 간섭(음량 합산) 연산 중...")
    with open(simulation_log_path, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("DIRECT_INFO") or line.startswith("Time") or line.startswith("Volume") or line.startswith("MODE"):
                 continue

            if line.endswith("Hz"):
                current_freq = int(line.replace("Hz", "").strip())
                if current_freq not in bands_raw_timeline:
                    bands_raw_timeline[current_freq] = {
                        'L': np.zeros(high_res_ir_length, dtype=np.float32),
                        'R': np.zeros(high_res_ir_length, dtype=np.float32)
                    }
                continue

            parts = line.split()
            if len(parts) >= 4:
                channel = parts[0]
                delay_idx = int(parts[1])
                gain = int(parts[2]) / vol_res

                if channel == 'L':
                    bands_raw_timeline[current_freq]['L'][delay_idx] += gain
                elif channel == 'R':
                    bands_raw_timeline[current_freq]['R'][delay_idx] += gain

    existing_freqs = sorted(list(bands_raw_timeline.keys()))
    log_freqs = [50] + existing_freqs + [12800]
    log_freqs = np.array(sorted(list(set(log_freqs))), dtype=np.float32)

    ir_matrix_l_640k = np.zeros((len(log_freqs), high_res_ir_length), dtype=np.float32)
    ir_matrix_r_640k = np.zeros((len(log_freqs), high_res_ir_length), dtype=np.float32)

    for idx, f_val in enumerate(log_freqs):
        if f_val in bands_raw_timeline:
            ir_matrix_l_640k[idx] = bands_raw_timeline[f_val]['L']
            ir_matrix_r_640k[idx] = bands_raw_timeline[f_val]['R']

    print("[3/6] 주파수 축 로그 스케일 인터폴레이션 프로필 생성...")
    target_freq_axis = np.logspace(np.log10(20), np.log10(20000), num=15)

    interp_func_l = interp.interp1d(np.log10(log_freqs), ir_matrix_l_640k, axis=0, bounds_error=False, fill_value=0.0)
    interp_func_r = interp.interp1d(np.log10(log_freqs), ir_matrix_r_640k, axis=0, bounds_error=False, fill_value=0.0)

    # 평균 -> 합산: 보강 간섭(여러 밴드의 기여) 손실 방지
    master_ir_l_640k = np.sum(interp_func_l(np.log10(target_freq_axis)), axis=0)
    master_ir_r_640k = np.sum(interp_func_r(np.log10(target_freq_axis)), axis=0)

    print("[3.5/6] 직접음 중복 방지 - 마스터 IR에서 직접음 위치 제거...")

    def remove_direct_peak(ir_640k, direct_idx, src_time_res, guard_sec=0.001):
        guard_samples = int(guard_sec * src_time_res)
        lo = max(0, direct_idx - guard_samples)
        hi = min(len(ir_640k), direct_idx + guard_samples + 1)
        ir_640k[lo:hi] = 0.0
        return ir_640k

    master_ir_l_640k = remove_direct_peak(master_ir_l_640k, direct_l_idx, src_time_res)
    master_ir_r_640k = remove_direct_peak(master_ir_r_640k, direct_r_idx, src_time_res)

    print("[4/6] 에너지 보존 방식으로 640kHz IR을 44.1kHz로 압축 중...")

    def downsample_energy_preserving(ir_640k, src_res, dst_sr):
        ratio = src_res / dst_sr  # 약 14.51
        out_len = int(np.ceil(len(ir_640k) / ratio)) + 1
        out = np.zeros(out_len, dtype=np.float64)
        nonzero_idx = np.nonzero(ir_640k)[0]
        if len(nonzero_idx) > 0:
            target_idx = (nonzero_idx / ratio).astype(np.int64)
            np.add.at(out, target_idx, ir_640k[nonzero_idx])
        return out.astype(np.float32)

    ir_left_44k = downsample_energy_preserving(master_ir_l_640k, src_time_res, target_sr)
    ir_right_44k = downsample_energy_preserving(master_ir_r_640k, src_time_res, target_sr)

    print("[5/6] 44.1kHz 타임라인 기준 직접음 정렬...")
    direct_l_sec = direct_l_idx / src_time_res
    direct_r_sec = direct_r_idx / src_time_res

    def apply_direct_sound(signal, delay_sec, sr):
        exact_samples = delay_sec * sr
        idx = int(np.floor(exact_samples))
        frac = exact_samples - idx
        delayed = np.zeros(len(signal) + idx + 2, dtype=np.float32)
        delayed[idx : idx + len(signal)] += signal * (1.0 - frac)
        delayed[idx + 1 : idx + 1 + len(signal)] += signal * frac
        return delayed

    direct_sound_l = apply_direct_sound(input_left, direct_l_sec, target_sr)
    direct_sound_r = apply_direct_sound(input_right, direct_r_sec, target_sr)

    print("[6/6] 최종 공간 잔향 FFT Convolution 연산 및 마스터링...")
    reverb_tail_l = fftconvolve(input_left, ir_left_44k)
    reverb_tail_r = fftconvolve(input_right, ir_right_44k)

    max_len = max(len(direct_sound_l), len(reverb_tail_l), len(direct_sound_r), len(reverb_tail_r))
    final_left = np.zeros(max_len, dtype=np.float32)
    final_right = np.zeros(max_len, dtype=np.float32)

    final_left[:len(direct_sound_l)] += direct_sound_l
    final_left[:len(reverb_tail_l)] += reverb_tail_l
    final_right[:len(direct_sound_r)] += direct_sound_r
    final_right[:len(reverb_tail_r)] += reverb_tail_r

    output_stereo = np.vstack((final_left, final_right)).T
    max_val = np.max(np.abs(output_stereo))
    if max_val > 0:
        output_stereo = output_stereo / max_val * 0.98

    output_stereo_int16 = (output_stereo * 32767.0).astype(np.int16)
    wavfile.write(output_wav_path, target_sr, output_stereo_int16)
    return True


if __name__ == "__main__":
    print("==========================================================")
    print(" 640kHz 보강간섭 선해결형 고해상도 리버브 엔진 (v5.0) ")
    print("==========================================================")

    wav_path = select_file_via_gui("합성할 원본 음원 파일(WAV) 선택", [("Wav Audio Files", "*.wav")])
    if not wav_path: sys.exit("[종료] 음원 파일 미선택")

    log_path = select_file_via_gui("리버브 시뮬레이션 결과 파일(TXT) 선택", [("Text Logs", "*.txt")])
    if not log_path: sys.exit("[종료] 로그 파일 미선택")

    suggested_name = os.path.splitext(os.path.basename(wav_path))[0] + "_HighResInterference_Reverb.wav"
    save_path = select_save_path_via_gui("합성 완료 파일 저장 위치 지정", suggested_name, [("Wav Audio Files", "*.wav")])
    if not save_path: sys.exit("[종료] 저장 경로 미지정")

    try:
        if apply_high_res_interference_reverb(wav_path, log_path, save_path, target_sr=44100):
            print("\n==================================================")
            print(" 640kHz 정밀 보강 간섭 병합 및 리버브 합성 완료!")
            print(f" 결과 파일 저장 위치: {save_path}")
            
            print("==================================================")

            root = tk.Tk()
            root.withdraw()
            messagebox.showinfo("합성 완료",
                                "다음 개선 사항이 모두 반영되었습니다:\n\n"
                                "1. 주파수 축 보간 결과를 합산(sum)으로 처리하여 보강 간섭 손실을 방지했습니다.\n"
                                "2. 마스터 IR에서 직접음 위치를 제거하여 직접음 중복 가산을 막았습니다.\n"
                                "3. resample_poly 대신 에너지 보존 누적 방식으로 640kHz -> 44.1kHz 다운샘플링하여 피크 손실을 방지했습니다.")
            root.destroy()
    except Exception as e:
        print(f"\n[에러 발생] 오디오 처리 실패: {e}")