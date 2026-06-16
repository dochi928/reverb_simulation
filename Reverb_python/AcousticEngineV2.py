import os
import math
import numpy as np
import scipy.io.wavfile as wav
from scipy.signal import lfilter
import matplotlib.pyplot as plt
from matplotlib.widgets import Button
import tkinter as tk
from tkinter import filedialog, messagebox


class ReverbEngineGUI:
    def __init__(self):
        self.fs = None
        self.dry_signal = None
        self.sim_data = None

        # V1 형식용
        self.direct_delay_ms = 0

        # V2 형식용
        self.is_v2 = False
        self.time_resolution = None
        self.vol_resolution = None
        self.direct_delay_sec_l = 0.0
        self.direct_delay_sec_r = 0.0

        self.default_sim_dir   = ""
        self.default_audio_dir = ""

        self.default_sim_path = self.get_default_sim_path()
        self.sim_data = self.parse_reverb_data(self.default_sim_path)

        self.fig, self.ax = plt.subplots(figsize=(12, 7))
        plt.subplots_adjust(bottom=0.3)

        status = self.get_status_text()
        self.ax.set_title(f"Stereo Reverb Engine (Feedback) | {status}")
        self.line_l, = self.ax.plot([], [], lw=0.5, color='#1f77b4', label='Left', alpha=0.8)
        self.line_r, = self.ax.plot([], [], lw=0.5, color='#ff7f0e', label='Right', alpha=0.6)
        self.ax.legend(loc='upper right')

        btn_w, btn_h = 0.2, 0.06
        self.btn_audio = Button(plt.axes([0.15, 0.15, btn_w, btn_h]), '1. Load Audio', color='lightgray')
        self.btn_data  = Button(plt.axes([0.40, 0.15, btn_w, btn_h]), '2. Load Sim Data', color='lightgray')
        self.btn_fft   = Button(plt.axes([0.15, 0.05, btn_w, btn_h]), '3. Analyze FFT', color='lightgray')
        self.btn_syn   = Button(plt.axes([0.40, 0.05, btn_w, btn_h]), '4. Start Synthesis', color='gold')

        self.btn_audio.on_clicked(self.select_audio)
        self.btn_data.on_clicked(self.select_sim_data)
        self.btn_fft.on_clicked(self.run_fft_analysis)
        self.btn_syn.on_clicked(self.run_synthesis)

    def get_default_sim_path(self):
        path = os.path.join(self.default_sim_dir, "reverb_result.txt")
        return path if os.path.exists(path) else None

    def get_status_text(self):
        if not self.sim_data:
            return "No Sim Data"
        if self.is_v2:
            return (f"V2 Loaded (L:{self.direct_delay_sec_l*1000:.2f}ms, "
                    f"R:{self.direct_delay_sec_r*1000:.2f}ms, "
                    f"TRes:{self.time_resolution:.0f}, VRes:{self.vol_resolution:.0f})")
        else:
            return f"Sim Loaded ({self.direct_delay_ms}ms)"

    def parse_reverb_data(self, file_path):
        if not file_path or not os.path.exists(file_path):
            return None

        self.is_v2 = False
        self.time_resolution = None
        self.vol_resolution = None
        self.direct_delay_sec_l = 0.0
        self.direct_delay_sec_r = 0.0
        self.direct_delay_ms = 0

        reverb_map = {}
        current_band = None
        direct_l_timeuS = None
        direct_r_timeuS = None

        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                for line in f:
                    line = line.strip()
                    if not line:
                        continue

                    if line.startswith("DIRECT_INFO_L"):
                        self.is_v2 = True
                        direct_l_timeuS = float(line.split()[2])
                        continue
                    if line.startswith("DIRECT_INFO_R"):
                        self.is_v2 = True
                        direct_r_timeuS = float(line.split()[2])
                        continue
                    if line.startswith("DIRECT_INFO"):
                        self.direct_delay_ms = float(line.split()[2])
                        continue

                    if line.startswith("Time Resolution"):
                        self.time_resolution = float(line.split(':')[1].strip())
                        continue
                    if line.startswith("Volume Resolution"):
                        self.vol_resolution = float(line.split(':')[1].strip())
                        continue

                    if line.startswith("MODE"):
                        continue

                    if line.endswith('Hz'):
                        current_band = line
                        reverb_map[current_band] = {'L': [], 'R': []}
                        continue

                    if (line.startswith('L') or line.startswith('R')) and current_band:
                        parts = line.split()
                        reverb_map[current_band][parts[0]].append((float(parts[1]), float(parts[2])))

            if self.is_v2:
                if not self.time_resolution:
                    self.time_resolution = 1.0
                if not self.vol_resolution:
                    self.vol_resolution = 1.0
                if direct_l_timeuS is not None:
                    self.direct_delay_sec_l = direct_l_timeuS / self.time_resolution
                if direct_r_timeuS is not None:
                    self.direct_delay_sec_r = direct_r_timeuS / self.time_resolution

            return reverb_map
        except Exception as e:
            print(f"Parse error: {e}")
            return None

    def get_band_weights(self, freq):
        bands = [50, 100, 200, 400, 800, 1600, 3200, 6400, 12800]
        if freq < bands[0] or freq > bands[-1]: return None
        for i in range(len(bands)-1):
            f_low, f_high = bands[i], bands[i+1]
            if f_low <= freq <= f_high:
                w_high = (math.log2(freq) - math.log2(f_low)) / (math.log2(f_high) - math.log2(f_low))
                return {"low": f_low, "high": f_high, "w_low": 1.0 - w_high, "w_high": w_high}
        return None

    def get_final_reverb_profile(self, freq, weights):
        if not weights or not self.sim_data: return None
        profile = {'L': [], 'R': []}
        l_key, h_key = f"{weights['low']}Hz", f"{weights['high']}Hz"

        if self.is_v2:
            direct_sec = {'L': self.direct_delay_sec_l, 'R': self.direct_delay_sec_r}
            time_div = self.time_resolution
            vol_div = self.vol_resolution
        else:
            direct_sec = {'L': self.direct_delay_ms / 1000.0, 'R': self.direct_delay_ms / 1000.0}
            time_div = 1000.0
            vol_div = 1000.0

        for side in ['L', 'R']:
            l_data = {d[0]: d[1] for d in self.sim_data.get(l_key, {}).get(side, [])}
            h_data = {d[0]: d[1] for d in self.sim_data.get(h_key, {}).get(side, [])}
            all_t = sorted(list(set(l_data.keys()) | set(h_data.keys())))
            for t in all_t:
                vol = (l_data.get(t, 0) * weights['w_low'] + h_data.get(t, 0) * weights['w_high']) / vol_div
                delay_sec = (t / time_div) - direct_sec[side]
                if delay_sec >= 0 and vol > 0: profile[side].append((delay_sec, vol))
        return profile

    def select_audio(self, event):
        root = tk.Tk(); root.withdraw()
        path = filedialog.askopenfilename(
            initialdir=self.default_audio_dir,
            filetypes=[("WAV files", "*.wav")]
        )
        root.destroy()
        if path:
            fs, data = wav.read(path)
            sig = data.astype(np.float32) / 32768.0
            if len(sig.shape) > 1: sig = np.mean(sig, axis=1)
            self.fs, self.dry_signal = fs, sig
            t = np.arange(len(sig)) / fs
            self.line_l.set_data(t, sig); self.line_r.set_data(t, sig)
            self.ax.set_xlim(0, t[-1]); self.ax.set_title(f"Loaded: {os.path.basename(path)} (Mono Mode)")
            plt.draw()

    def select_sim_data(self, event):
        root = tk.Tk(); root.withdraw()
        path = filedialog.askopenfilename(
            initialdir=self.default_sim_dir,
            filetypes=[("Text files", "*.txt")]
        )
        root.destroy()
        if path:
            self.sim_data = self.parse_reverb_data(path)
            self.ax.set_title(f"Sim Updated | {self.get_status_text()}")
            plt.draw()

    def run_fft_analysis(self, event):
        if self.dry_signal is None: return
        n = len(self.dry_signal)
        fft_res = np.fft.fft(self.dry_signal)
        mags = np.abs(fft_res)[:n//2]
        freqs = np.fft.fftfreq(n, 1/self.fs)[:n//2]
        max_mag = np.max(mags)
        sig_list = []
        for f, m in zip(np.round(freqs).astype(int), mags):
            if m >= max_mag * 0.001:
                w = self.get_band_weights(f)
                if w: sig_list.append((f, m, w))
        self.display_fft_results(freqs, mags, max_mag*0.001, sig_list)

    def display_fft_results(self, freqs, mags, threshold, sig_list):
        fig = plt.figure(figsize=(15, 7))
        gs = fig.add_gridspec(1, 2, width_ratios=[2, 1])
        ax_spec = fig.add_subplot(gs[0])
        ax_spec.plot(freqs, mags, color='purple', lw=0.5); ax_spec.axhline(threshold, color='red', ls='--')
        ax_spec.set_yscale('log'); ax_spec.set_xlim(0, 10000); ax_spec.set_title("FFT Spectrum")
        ax_txt = fig.add_subplot(gs[1]); ax_txt.axis('off')
        txt = "--- Significant Frequencies ---\n\n"
        for f, m, w in sorted(sig_list, key=lambda x: x[1], reverse=True)[:25]:
            p = self.get_final_reverb_profile(f, w)
            d = f"{p['L'][0][0]*1000:.1f}ms" if p['L'] else "N/A"
            txt += f"{f:5d}Hz | Mag:{m:7.1f} | Ref:{d}\n"
        ax_txt.text(0, 1, txt, va='top', family='monospace', fontsize=9); plt.show()

    # ------------------------------------------------------------------
    # 피드백(재귀) 기반 합성
    # ------------------------------------------------------------------
    def build_gain_table(self, side):
        """
        모든 밴드의 (delay_samples, gain) 튜플을 모아서
        delay_samples별로 gain을 합산한 sparse 테이블을 만든다.
        같은 delay_samples에 여러 밴드/레이가 모이면 합산됨.
        """
        if self.is_v2:
            direct_sec = self.direct_delay_sec_l if side == 'L' else self.direct_delay_sec_r
            time_div = self.time_resolution
            vol_div = self.vol_resolution
        else:
            direct_sec = self.direct_delay_ms / 1000.0
            time_div = 1000.0
            vol_div = 1000.0

        gain_dict = {}  # delay_samples -> gain sum
        num_bands = len(self.sim_data.keys())

        for band_key in self.sim_data.keys():
            for t_raw, vol_raw in self.sim_data[band_key][side]:
                delay_sec = (t_raw / time_div) - direct_sec
                if delay_sec <= 0:
                    continue
                delay_samples = int(round(delay_sec * self.fs))
                if delay_samples <= 0:
                    continue
                gain = (vol_raw / vol_div) / num_bands
                gain_dict[delay_samples] = gain_dict.get(delay_samples, 0.0) + gain

        if not gain_dict:
            return np.array([], dtype=np.int64), np.array([], dtype=np.float64)

        delays = np.array(sorted(gain_dict.keys()), dtype=np.int64)
        gains = np.array([gain_dict[d] for d in delays], dtype=np.float64)
        return delays, gains

    def apply_feedback_reverb(self, dry, delays, gains, tail_seconds=1.0):
        """
        y[n] = x[n] + sum_k gains[k] * y[n - delays[k]]

        IIR 차분방정식 형태:
          a[0]*y[n] - sum_k gains[k]*y[n-delays[k]] = x[n]
          a[0] = 1, a[delays[k]] = -gains[k]

        scipy.signal.lfilter(b, a, x) 는
          a[0]*y[n] = b[0]*x[n] - sum_{k>=1} a[k]*y[n-k]
        형태이므로 그대로 매핑 가능.
        """
        if len(delays) == 0:
            return dry.copy()

        max_delay = int(delays.max())
        tail_len = int(self.fs * tail_seconds)
        pad_len = max_delay + tail_len

        x = np.concatenate([dry, np.zeros(pad_len, dtype=np.float64)])

        a = np.zeros(max_delay + 1, dtype=np.float64)
        a[0] = 1.0
        for d, g in zip(delays, gains):
            a[d] -= g

        b = np.array([1.0], dtype=np.float64)

        y = lfilter(b, a, x)
        return y

    def run_synthesis(self, event):
        if self.dry_signal is None or self.sim_data is None: return

        print("Building feedback gain tables...")
        delays_l, gains_l = self.build_gain_table('L')
        delays_r, gains_r = self.build_gain_table('R')

        print(f"  L: {len(delays_l)} taps, sum(gain)={gains_l.sum():.4f}, "
              f"max(gain)={gains_l.max() if len(gains_l) else 0:.6f}")
        print(f"  R: {len(delays_r)} taps, sum(gain)={gains_r.sum():.4f}, "
              f"max(gain)={gains_r.max() if len(gains_r) else 0:.6f}")

        # 안정성 체크 (발산 방지)
        for side, gains in [('L', gains_l), ('R', gains_r)]:
            total = gains.sum() if len(gains) else 0.0
            if total >= 1.0:
                print(f"  [경고] {side} 채널 gain 합계 {total:.4f} >= 1.0 -> 발산 위험, 자동 스케일 적용")
                scale = 0.95 / total
                if side == 'L':
                    gains_l = gains_l * scale
                else:
                    gains_r = gains_r * scale

        print("Applying recursive (feedback) reverb...")
        out_l = self.apply_feedback_reverb(self.dry_signal, delays_l, gains_l, tail_seconds=1.5)
        out_r = self.apply_feedback_reverb(self.dry_signal, delays_r, gains_r, tail_seconds=1.5)

        max_len = max(len(out_l), len(out_r))
        if len(out_l) < max_len:
            out_l = np.pad(out_l, (0, max_len - len(out_l)))
        if len(out_r) < max_len:
            out_r = np.pad(out_r, (0, max_len - len(out_r)))

        out = np.stack([out_l, out_r], axis=1)

        if not np.all(np.isfinite(out)):
            print("  [오류] 출력에 NaN/Inf 발생 - gain 발산 가능성. 0으로 대체")
            out = np.nan_to_num(out)

        peak = np.max(np.abs(out))
        if peak > 0:
            out = out / (peak + 1e-9) * 0.98

        root = tk.Tk(); root.withdraw()
        path = filedialog.asksaveasfilename(
            initialdir=self.default_audio_dir,
            defaultextension=".wav",
            filetypes=[("WAV files", "*.wav")],
            title="Save Synthesized Audio (Feedback)"
        )
        root.destroy()

        if path:
            wav.write(path, self.fs, (out * 32767).astype(np.int16))
            messagebox.showinfo("Done", f"Synthesis Completed & Saved!\nPath: {path}")

            self.ax.clear()
            t = np.arange(len(out)) / self.fs
            self.ax.plot(t, out[:, 0], lw=0.5, color='#1f77b4', label='Left Reverb', alpha=0.7)
            self.ax.plot(t, out[:, 1], lw=0.5, color='#ff7f0e', label='Right Reverb', alpha=0.5)
            self.ax.legend()
            self.ax.set_title(f"Feedback Reverb Output | {self.get_status_text()}")
            plt.draw()

    def show(self): plt.show()


if __name__ == "__main__":
    app = ReverbEngineGUI(); app.show()
