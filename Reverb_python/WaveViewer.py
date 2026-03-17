import numpy as np
import scipy.io.wavfile as wav
import matplotlib.pyplot as plt
from matplotlib.widgets import Button
import tkinter as tk
from tkinter import filedialog
import os

class AudioAnalyzer:
    def __init__(self):
        # 레이아웃 설정: 3행 1열 구조
        self.fig = plt.figure(figsize=(12, 9))
        plt.subplots_adjust(hspace=0.4, bottom=0.15)

        self.ax_wave = self.fig.add_subplot(3, 1, 1)  # Time Domain
        self.ax_spec = self.fig.add_subplot(3, 1, 2)  # Frequency Domain
        self.ax_gram = self.fig.add_subplot(3, 1, 3)  # Spectrogram

        # 버튼 설정
        self.btn_load = Button(plt.axes([0.4, 0.03, 0.2, 0.05]), 'Load Audio File', color='lightblue')
        self.btn_load.on_clicked(self.load_audio)

        self.fs = None
        self.data = None

    def load_audio(self, event):
        root = tk.Tk(); root.withdraw()
        path = filedialog.askopenfilename(filetypes=[("WAV files", "*.wav")])
        root.destroy()

        if path:
            self.fs, data = wav.read(path)
            # 스테레오일 경우 분석을 위해 모노로 합산
            if len(data.shape) > 1:
                self.data = data.astype(np.float32).mean(axis=1)
            else:
                self.data = data.astype(np.float32)

            # 정규화
            self.data /= (np.max(np.abs(self.data)) + 1e-6)

            self.analyze(os.path.basename(path))

    def analyze(self, filename):
        # 1. 파형 분석 (Time Domain)
        self.ax_wave.clear()
        time = np.arange(len(self.data)) / self.fs
        self.ax_wave.plot(time, self.data, lw=0.5, color='dodgerblue')
        self.ax_wave.set_title(f"Waveform: {filename}")
        self.ax_wave.set_xlabel("Time (s)")
        self.ax_wave.set_ylabel("Amplitude")
        self.ax_wave.grid(True, alpha=0.3)

        # 2. 주파수 분석 (Frequency Domain - FFT)
        self.ax_spec.clear()
        n = len(self.data)
        freqs = np.fft.fftfreq(n, 1/self.fs)[:n//2]
        mags = np.abs(np.fft.fft(self.data))[:n//2]

        self.ax_spec.plot(freqs, 20 * np.log10(mags + 1e-6), lw=0.5, color='crimson')
        self.ax_spec.set_title("Frequency Spectrum (Magnitude)")
        self.ax_spec.set_xlabel("Frequency (Hz)")
        self.ax_spec.set_ylabel("Magnitude (dB)")
        self.ax_spec.set_xlim(0, 20000) # 20kHz까지 표시
        self.ax_spec.grid(True, alpha=0.3)

        # 3. 스펙트로그램 (Spectrogram - Time vs Freq)
        self.ax_gram.clear()
        # NFFT를 조절하여 해상도 변경 가능
        self.ax_gram.specgram(self.data, Fs=self.fs, NFFT=1024, noverlap=512, cmap='magma')
        self.ax_gram.set_title("Spectrogram (Energy distribution over time)")
        self.ax_gram.set_xlabel("Time (s)")
        self.ax_gram.set_ylabel("Frequency (Hz)")
        self.ax_gram.set_ylim(0, 10000) # 가청 영역 중심 시각화

        self.fig.canvas.draw()

    def show(self):
        plt.show()

if __name__ == "__main__":
    analyzer = AudioAnalyzer()
    analyzer.show()