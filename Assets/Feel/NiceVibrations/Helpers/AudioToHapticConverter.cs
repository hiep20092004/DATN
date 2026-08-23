using System;
using System.Collections.Generic;
using System.Linq;
using Lofelt.NiceVibrations;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace MoreMountains.FeedbacksForThirdParty
{
	/// <summary>
	/// A class used to convert an AudioClip into a .haptic file
	/// </summary>
	public class AudioToHapticConverter
	{
		/// <summary>
		/// Converts an AudioClip into a HapticClip and returns it
		/// </summary>
		/// <param name="audioClip">Input audio clip.</param>
		/// <param name="outputFolder">Folder where the .haptic file will be created.</param>
		/// <param name="outputFileName">Output file name (with .haptic extension).</param>
		/// <param name="normalizeAmplitude">Whether to normalize amplitude motor curve.</param>
		/// <param name="normalizeAmplitudeFactor">Target max amplitude after normalization.</param>
		/// <param name="normalizeFrequency">Whether to normalize frequency motor curve.</param>
		/// <param name="normalizeFrequencyFactor">Target max frequency after normalization.</param>
		/// <param name="sampleCount">How many sampled points to generate.</param>
		/// <param name="trimStartSeconds">Start time in seconds for conversion range.</param>
		/// <param name="trimEndSeconds">End time in seconds for conversion range. Values <= 0 use clip end.</param>
		/// <param name="amplitudeThreshold">Amplitude values below this threshold are considered silence.</param>
		/// <returns>Generated haptic data, or null if conversion fails.</returns>
		public static NVHapticData GenerateHapticFile(AudioClip audioClip, string outputFolder, string outputFileName,
			bool normalizeAmplitude = false, float normalizeAmplitudeFactor = 1f,
			bool normalizeFrequency = false, float normalizeFrequencyFactor = 1f,
			int sampleCount = 100,
			float trimStartSeconds = 0f, float trimEndSeconds = -1f,
			float amplitudeThreshold = 0f)
		{
			#if UNITY_EDITOR
			if (audioClip == null)
			{
				Debug.LogError("No AudioClip assigned! Please assign one.");
				return null;
			}

			string outputPath = Path.Combine(outputFolder, outputFileName);

			try
			{
				float clipLength = audioClip.length;
				float trimStart = Mathf.Clamp(trimStartSeconds, 0f, clipLength);
				float trimEnd = trimEndSeconds <= 0f ? clipLength : Mathf.Clamp(trimEndSeconds, 0f, clipLength);
				if (trimEnd <= trimStart)
				{
					Debug.LogError("Trim end time must be greater than trim start time.");
					return null;
				}

				float trimmedDuration = trimEnd - trimStart;
				float clampedAmplitudeThreshold = Mathf.Clamp01(amplitudeThreshold);
				sampleCount = Mathf.Max(2, sampleCount);
				float[] samples = new float[audioClip.samples * audioClip.channels];
				audioClip.GetData(samples, 0);

				List<NVAmplitudePoint> amplitudePoints = new List<NVAmplitudePoint>(sampleCount);
				List<NVFrequencyPoint> frequencyPoints = new List<NVFrequencyPoint>(sampleCount);

				// amplitude

				for (int i = 0; i < sampleCount; i++)
				{
					float localTime = (trimmedDuration / (sampleCount - 1)) * i;
					float absoluteTime = trimStart + localTime;
					int sampleIndex = Mathf.Min((int)(absoluteTime * audioClip.frequency) * audioClip.channels,
						samples.Length - audioClip.channels);

					float sum = 0f;
					for (int c = 0; c < audioClip.channels; c++)
					{
						sum += Mathf.Abs(samples[sampleIndex + c]);
					}

					float amplitude = Mathf.Clamp01(sum / audioClip.channels);
					if (amplitude < clampedAmplitudeThreshold)
					{
						amplitude = 0f;
					}

					float emphasisAmplitude = Mathf.Max(amplitude, 0f);
					NVEmphasis emphasis = new NVEmphasis()
					{
						amplitude = emphasisAmplitude,
						frequency = emphasisAmplitude
					};

					amplitudePoints.Add(new NVAmplitudePoint()
					{
						time = localTime,
						amplitude = amplitude,
						emphasis = emphasis
					});
				}

				// frequency

				int frameSize = 1024;

				for (int i = 0; i < sampleCount; i++)
				{
					float localTime = (trimmedDuration / (sampleCount - 1)) * i;
					float absoluteTime = trimStart + localTime;
					int monoStartIndex = Mathf.Clamp((int)(absoluteTime * audioClip.frequency), 0, audioClip.samples - 1);
					int frameLength = Mathf.Min(frameSize, audioClip.samples - monoStartIndex);

					float frequency = 0f;
					if (frameLength > 1)
					{
						float[] frame = new float[frameLength];
						float maxAmplitude = 0f;

						for (int sampleOffset = 0; sampleOffset < frameLength; sampleOffset++)
						{
							float monoSample = 0f;
							int baseIndex = (monoStartIndex + sampleOffset) * audioClip.channels;
							for (int c = 0; c < audioClip.channels; c++)
							{
								monoSample += samples[baseIndex + c];
							}

							monoSample /= audioClip.channels;
							frame[sampleOffset] = monoSample;
							maxAmplitude = Mathf.Max(maxAmplitude, Mathf.Abs(monoSample));
						}

						if (maxAmplitude >= clampedAmplitudeThreshold)
						{
							frequency = EstimateFrequencyZCR(frame, audioClip.frequency);
						}
					}

					frequencyPoints.Add(new NVFrequencyPoint()
					{
						time = localTime,
						frequency = frequency
					});
				}

				NVHapticFile hapticFile = new NVHapticFile()
				{
					version = new NVVersion()
					{
						major = 1,
						minor = 0,
						patch = 0
					},
					metadata = new NVMetadata()
					{
						editor = "Feel",
						author = "More Mountains",
						source = audioClip.name,
						project = "Feel",
						tags = new List<string> { "converted", "audio" },
						description = "Haptic data generated by Feel from AudioClip"
					},
					signals = new NVSignals()
					{
						continuous = new NVContinuous()
						{
							envelopes = new NVEnvelopes()
							{
								amplitude = amplitudePoints,
								frequency = frequencyPoints
							}
						}
					}
				};

				string json = JsonUtility.ToJson(hapticFile, true);
				File.WriteAllText(outputPath, json);
				UnityEditor.AssetDatabase.Refresh();

				HapticClip haptic = AssetDatabase.LoadAssetAtPath<HapticClip>(outputPath);
				Debug.Log($"Haptic file generated and saved to: {outputPath}");

				NVHapticData data = new NVHapticData();
				data.Clip = haptic;
				data.SampleCount = sampleCount;
				data.AmplitudePoints = amplitudePoints;
				data.FrequencyPoints = frequencyPoints;

				haptic.gamepadRumble = ConvertRumbleData(data, haptic.gamepadRumble.totalDurationMs, normalizeAmplitude,
					normalizeAmplitudeFactor, normalizeFrequency, normalizeFrequencyFactor);

				data.RumbleData = haptic.gamepadRumble;

				return data;
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to generate haptic file: " + e.Message);
			}
			#endif
			return null;
		}

		/// <summary>
		/// converts amplitude & frequency to rumble data
		/// </summary>
		/// <param name="data"></param>
		/// <param name="totalDurationMs"></param>
		/// <param name="normalizeAmplitude"></param>
		/// <param name="normalizeAmplitudeFactor"></param>
		/// <param name="normalizeFrequency"></param>
		/// <param name="normalizeFrequencyFactor"></param>
		/// <returns></returns>
		protected static GamepadRumble ConvertRumbleData(NVHapticData data, int totalDurationMs,
			bool normalizeAmplitude = false, float normalizeAmplitudeFactor = 1f,
			bool normalizeFrequency = false, float normalizeFrequencyFactor = 1f)
		{
			GamepadRumble result = new GamepadRumble();
			result.totalDurationMs = totalDurationMs;

			result.durationsMs = new int[data.AmplitudePoints.Count];
			result.highFrequencyMotorSpeeds = new float[data.AmplitudePoints.Count];
			result.lowFrequencyMotorSpeeds = new float[data.AmplitudePoints.Count];

			for (int i = 0; i < data.AmplitudePoints.Count; i++)
			{
				result.durationsMs[i] = Mathf.RoundToInt(totalDurationMs / data.AmplitudePoints.Count);
				result.highFrequencyMotorSpeeds[i] = data.AmplitudePoints[i].emphasis.amplitude;
				result.lowFrequencyMotorSpeeds[i] =
					data.FrequencyPoints[i].frequency * result.highFrequencyMotorSpeeds[i];
			}

			// normalizing
			if (normalizeAmplitude)
			{
				result.highFrequencyMotorSpeeds = Normalize(result.highFrequencyMotorSpeeds, normalizeAmplitudeFactor);
			}

			if (normalizeFrequency)
			{
				result.lowFrequencyMotorSpeeds = Normalize(result.lowFrequencyMotorSpeeds, normalizeFrequencyFactor);
			}

			return result;
		}

		/// <summary>
		/// Normalizes the curve based on a specified max value
		/// </summary>
		/// <param name="data"></param>
		/// <param name="maxDesiredValue"></param>
		/// <returns></returns>
		protected static float[] Normalize(float[] data, float maxDesiredValue)
		{
			float currentMax = data.Max();

			if (currentMax > 0f)
			{
				float scaleFactor = maxDesiredValue / currentMax;

				for (int i = 0; i < data.Length; i++)
				{
					data[i] *= scaleFactor;
				}
			}

			return data;
		}

		/// <summary>
		/// Estimates the frequency using zero crossing
		/// </summary>
		/// <param name="frame"></param>
		/// <param name="sampleRate"></param>
		/// <returns></returns>
		protected static float EstimateFrequencyZCR(float[] frame, int sampleRate)
		{
			int zeroCrossings = 0;
			for (int i = 1; i < frame.Length; i++)
			{
				if ((frame[i - 1] >= 0 && frame[i] < 0) || (frame[i - 1] < 0 && frame[i] >= 0))
				{
					zeroCrossings++;
				}
			}

			float duration = frame.Length / (float)sampleRate;
			float estimatedFreq = (zeroCrossings / (2f * duration));
			float normalized = Mathf.Clamp01(estimatedFreq / 1000f);
			return normalized;
		}
	}
}