using System;

namespace AntigravityLauncher
{
    public static class AudioSynthesizer
    {
        public static byte[] GenerateSoundtrackWav(double totalDuration, double timeScale)
        {
            int sampleRate = 44100;
            double playDuration = totalDuration / (timeScale > 0.0 ? timeScale : 1.0);
            int totalSamples = (int)(sampleRate * playDuration);
            int channels = 2;
            int byteCount = totalSamples * channels * 2;
            byte[] wav = new byte[44 + byteCount];

            WriteAscii(wav, 0, "RIFF");
            WriteInt32(wav, 4, 36 + byteCount);
            WriteAscii(wav, 8, "WAVE");
            WriteAscii(wav, 12, "fmt ");
            WriteInt32(wav, 16, 16);
            WriteInt16(wav, 20, 1); // PCM
            WriteInt16(wav, 22, (short)channels);
            WriteInt32(wav, 24, sampleRate);
            WriteInt32(wav, 28, sampleRate * channels * 2);
            WriteInt16(wav, 32, (short)(channels * 2));
            WriteInt16(wav, 34, 16);
            WriteAscii(wav, 36, "data");
            WriteInt32(wav, 40, byteCount);

            // Harmonic intervals (C major 9th luminous palette)
            double[] padFreqs = new double[] { 130.81, 196.00, 246.94, 293.66, 329.63 };
            double[] pingTimes = new double[] { 8.40, 8.95, 9.50, 10.05, 10.65 };
            double[] pingFreqs = new double[] { 880.00, 987.77, 1174.66, 1318.51, 1567.98 };

            for (int i = 0; i < totalSamples; i++)
            {
                double realT = (double)i / sampleRate;
                double t = realT * timeScale;
                double left = 0.0;
                double right = 0.0;

                // 1. Act 1-2: Deep stereo sub-bass gravitational drone & rising tension shimmer (0.0 - 5.8s)
                if (t < 6.4)
                {
                    double inEnv = SmoothStep(0.0, 0.9, t);
                    double outEnv = 1.0 - SmoothStep(5.25, 6.35, t);
                    double lfo = 0.85 + 0.15 * Math.Sin(2.0 * Math.PI * 0.35 * realT);
                    double sub = Math.Sin(2.0 * Math.PI * 48.99 * realT) * 0.14
                               + Math.Sin(2.0 * Math.PI * 73.42 * realT) * 0.07;

                    double tension = SmoothStep(2.2, 5.45, t);
                    double shimmerFreq = 196.0 + tension * 98.0;
                    double shimmerL = Math.Sin(2.0 * Math.PI * (shimmerFreq - 0.6) * realT) * tension * 0.055;
                    double shimmerR = Math.Sin(2.0 * Math.PI * (shimmerFreq + 0.6) * realT) * tension * 0.055;

                    left += (sub * lfo + shimmerL) * inEnv * outEnv;
                    right += (sub * lfo + shimmerR) * inEnv * outEnv;
                }

                // 2. Act 3: Gravity Inversion Drop + Glass Dispersion Chord (5.45s - 9.0s)
                if (t >= 5.40 && t < 9.20)
                {
                    double dt = (t - 5.40) / timeScale;
                    double attack = Clamp01(dt / 0.025);
                    double subDropEnv = Math.Exp(-dt * 2.2);
                    double subPitch = 110.0 * Math.Exp(-dt * 3.8) + 42.0;
                    double subDrop = Math.Sin(2.0 * Math.PI * subPitch * dt) * subDropEnv * 0.24;

                    double chordEnv = Math.Exp(-dt * 1.05);
                    double chordL = Math.Sin(2.0 * Math.PI * 261.63 * dt) * 0.09
                                  + Math.Sin(2.0 * Math.PI * 392.00 * dt) * 0.07
                                  + Math.Sin(2.0 * Math.PI * 587.33 * dt) * 0.05;
                    double chordR = Math.Sin(2.0 * Math.PI * 262.40 * dt) * 0.09
                                  + Math.Sin(2.0 * Math.PI * 391.20 * dt) * 0.07
                                  + Math.Sin(2.0 * Math.PI * 659.25 * dt) * 0.05;

                    left += (subDrop + chordL * chordEnv) * attack;
                    right += (subDrop + chordR * chordEnv) * attack;
                }

                // 3. Act 4-5: Airy Fluent/Keynote Major-9th Ambient Pad (6.2s - 14.8s)
                if (t >= 6.0 && t < 14.95)
                {
                    double padIn = SmoothStep(6.0, 7.8, t);
                    double padOut = 1.0 - SmoothStep(13.2, 14.9, t);
                    double padEnv = padIn * padOut * 0.048;

                    double pL = 0.0;
                    double pR = 0.0;
                    for (int k = 0; k < padFreqs.Length; k++)
                    {
                        double detune = (k % 2 == 0 ? 0.38 : -0.38);
                        double weight = 1.0 - k * 0.12;
                        pL += Math.Sin(2.0 * Math.PI * (padFreqs[k] + detune) * realT) * weight;
                        pR += Math.Sin(2.0 * Math.PI * (padFreqs[k] - detune) * realT) * weight;
                    }
                    left += pL * padEnv;
                    right += pR * padEnv;
                }

                // 4. Act 4: Subtle Crystalline Telemetry Pings (8.4s - 11.2s)
                for (int p = 0; p < pingTimes.Length; p++)
                {
                    if (t >= pingTimes[p] && t < pingTimes[p] + 0.9)
                    {
                        double pt = (t - pingTimes[p]) / timeScale;
                        double pAttack = Clamp01(pt / 0.008);
                        double pDecay = Math.Exp(-pt * 8.5);
                        double val = Math.Sin(2.0 * Math.PI * pingFreqs[p] * pt) * pAttack * pDecay * 0.045;
                        double pan = (p % 2 == 0) ? 0.72 : 0.28;
                        left += val * (1.0 - pan * 0.45);
                        right += val * (0.55 + pan * 0.45);
                    }
                }

                // 5. Act 5: Signature Luminous Lock-In Harmonic Chime (11.55s - 14.9s)
                if (t >= 11.52)
                {
                    double ct = (t - 11.52) / timeScale;
                    double cAttack = Clamp01(ct / 0.015);
                    double d1 = Math.Exp(-ct * 1.85);
                    double d2 = Math.Exp(-ct * 2.55);
                    double chimeL = Math.Sin(2.0 * Math.PI * 523.25 * ct) * d1 * 0.13
                                  + Math.Sin(2.0 * Math.PI * 783.99 * ct) * d2 * 0.09
                                  + Math.Sin(2.0 * Math.PI * 1046.50 * ct) * d2 * 0.06
                                  + Math.Sin(2.0 * Math.PI * 1567.98 * ct) * Math.Exp(-ct * 3.8) * 0.035;
                    double chimeR = Math.Sin(2.0 * Math.PI * 523.85 * ct) * d1 * 0.13
                                  + Math.Sin(2.0 * Math.PI * 783.10 * ct) * d2 * 0.09
                                  + Math.Sin(2.0 * Math.PI * 1047.30 * ct) * d2 * 0.06
                                  + Math.Sin(2.0 * Math.PI * 1566.80 * ct) * Math.Exp(-ct * 3.8) * 0.035;
                    left += chimeL * cAttack;
                    right += chimeR * cAttack;
                }

                double masterFade = 1.0 - SmoothStep(playDuration - 0.35, playDuration, realT);
                left = Clamp(left * masterFade, -0.92, 0.92);
                right = Clamp(right * masterFade, -0.92, 0.92);

                short pcmL = (short)(left * 32767.0);
                short pcmR = (short)(right * 32767.0);
                int idx = 44 + i * 4;
                wav[idx] = (byte)(pcmL & 0xFF);
                wav[idx + 1] = (byte)((pcmL >> 8) & 0xFF);
                wav[idx + 2] = (byte)(pcmR & 0xFF);
                wav[idx + 3] = (byte)((pcmR >> 8) & 0xFF);
            }

            return wav;
        }

        private static double Clamp(double val, double min, double max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        private static double Clamp01(double val)
        {
            return Clamp(val, 0.0, 1.0);
        }

        private static double SmoothStep(double edge0, double edge1, double x)
        {
            double u = Clamp01((x - edge0) / (edge1 - edge0));
            return u * u * (3.0 - 2.0 * u);
        }

        private static void WriteAscii(byte[] buf, int offset, string s)
        {
            for (int i = 0; i < s.Length; i++) buf[offset + i] = (byte)s[i];
        }

        private static void WriteInt32(byte[] buf, int offset, int v)
        {
            buf[offset] = (byte)(v & 0xFF);
            buf[offset + 1] = (byte)((v >> 8) & 0xFF);
            buf[offset + 2] = (byte)((v >> 16) & 0xFF);
            buf[offset + 3] = (byte)((v >> 24) & 0xFF);
        }

        private static void WriteInt16(byte[] buf, int offset, short v)
        {
            buf[offset] = (byte)(v & 0xFF);
            buf[offset + 1] = (byte)((v >> 8) & 0xFF);
        }
    }
}
