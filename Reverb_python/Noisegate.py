import numpy as np
import scipy.io.wavfile as wav
import matplotlib.pyplot as plt
from matplotlib.widgets import Button, Slider
import tkinter as tk
from tkinter import filedialog, messagebox
import os

class NoiseGateUI:
    def __init__(self):
        self.fs = None
        self.raw_signal = None       # 파일에서 읽은 원본
        self.gained_signal = None    # 게인이 적용된 상태
        self.processed_signal = None # 게인 + 게이트 적용 완료

        # 1. 메인 UI 설정
        self.fig, self.ax = plt.subplots(figsize=(12, 7))
        plt.subplots_adjust(bottom=0.4) # 슬라이더 공간 확보
        self.ax.set_title("Noise Gate Pro: Load Audio to Start")

        self.line_orig, = self.ax.plot([], [], lw=0.5, color='gray', alpha=0.3, label='Gained (Input)')
        self.line_proc, = self.ax.plot([], [], lw=0.5, color='cyan', label='Gated (Output)')
        self.ax.legend(loc='upper right')

        # 2. 슬라이더 설정 (0.1 단위 이산화를 위해 valstep 적용)
        # Pre-Gain 슬라이더 추가 (-20dB ~ +20dB)
        ax_gain = plt.axes([0.2, 0.25, 0.6, 0.03])
        self.slider_gain = Slider(ax_gain, 'Pre-Gain (dB)', -20.0, 20.0, valinit=0.0, valstep=0.1, color='lightblue')

        # Threshold 슬라이더 (-90dB ~ 0dB)
        ax_thresh = plt.axes([0.2, 0.20, 0.6, 0.03])
        self.slider_thresh = Slider(ax_thresh, 'Threshold (dB)', -90.0, 0.0, valinit=-45.0, valstep=0.1, color='orange')

        # Release 슬라이더 (0ms ~ 500ms, 1ms 단위가 적당함)
        ax_release = plt.axes([0.2, 0.15, 0.6, 0.03])
        self.slider_release = Slider(ax_release, 'Release (ms)', 0, 500, valinit=50, valstep=1.0, color='lightgreen')

        # 3. 버튼 설정
        self.btn_load = Button(plt.axes([0.2, 0.05, 0.2, 0.06]), '1. Load Audio', color='lightgray')
        self.btn_save = Button(plt.axes([0.6, 0.05, 0.2, 0.06]), '2. Save Result', color='gold')

        self.btn_load.on_clicked(self.load_audio)
        self.btn_save.on_clicked(self.save_audio)

        # 슬라이더 이벤트 연결
        self.slider_gain.on_changed(self.update)
        self.slider_thresh.on_changed(self.update)
        self.slider_release.on_changed(self.update)

    def load_audio(self, event):
        root = tk.Tk(); root.withdraw()
        path = filedialog.askopenfilename(filetypes=[("WAV files", "*.wav")])
        root.destroy()
        if path:
            self.fs, data = wav.read(path)
            sig = data.astype(np.float32) / 32768.0
            if len(sig.shape) > 1: sig = np.mean(sig, axis=1) # UI 편의상 모노 합산
            self.raw_signal = sig
            self.update(None)

    def apply_gate(self, signal, threshold_db, release_ms):
        thresh_linear = 10 ** (threshold_db / 20)
        envelope = np.abs(signal)
        mask = (envelope > thresh_linear).astype(float)

        release_samples = int((release_ms / 1000.0) * self.fs)
        if release_samples > 0:
            # 릴리즈 구현 (벡터 연산 대신 루프로 부드러운 감쇄 처리)
            for i in range(1, len(mask)):
                if mask[i] < mask[i-1]:
                    mask[i] = max(0, mask[i-1] - (1.0 / release_samples))
                elif mask[i] > 0:
                    mask[i] = 1.0
        return signal * mask

    def update(self, val):
        if self.raw_signal is None: return

        # 1. Pre-Gain 적용
        gain_db = self.slider_gain.val
        gain_linear = 10 ** (gain_db / 20)
        self.gained_signal = self.raw_signal * gain_linear

        # 2. Noise Gate 적용
        thresh = self.slider_thresh.val
        release = self.slider_release.val
        self.processed_signal = self.apply_gate(self.gained_signal, thresh, release)

        # 3. 그래프 업데이트
        t = np.arange(len(self.raw_signal)) / self.fs
        self.line_orig.set_data(t, self.gained_signal)
        self.line_proc.set_data(t, self.processed_signal)

        self.ax.set_xlim(0, t[-1])
        # 게인에 따라 축 범위 조정 (피크가 보일 수 있게)
        max_view = max(1.1, np.max(np.abs(self.gained_signal)) if len(self.gained_signal)>0 else 1.1)
        self.ax.set_ylim(-max_view, max_view)

        self.ax.set_title(f"Gain: {gain_db:+.1f}dB | Thresh: {thresh:.1f}dB | Release: {release:.0f}ms")
        plt.draw()

    def save_audio(self, event):
        if self.processed_signal is None: return

        # 세이브 전 최종 피크 체크 (Clipping 방지)
        peak = np.max(np.abs(self.processed_signal))
        save_signal = self.processed_signal
        if peak > 1.0:
            if messagebox.askyesno("Clipping Warning", "결과물이 0dB를 초과합니다. 노멀라이즈 후 저장할까요?"):
                save_signal /= (peak + 1e-6)

        root = tk.Tk(); root.withdraw()
        path = filedialog.asksaveasfilename(defaultextension=".wav")
        root.destroy()
        if path:
            out = (save_signal * 32767).astype(np.int16)
            wav.write(path, self.fs, out)
            messagebox.showinfo("Success", f"Saved to:\n{path}")

    def show(self):
        plt.show()

if __name__ == "__main__":
    app = NoiseGateUI()
    app.show()