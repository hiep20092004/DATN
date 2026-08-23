using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace WaterFlow.Game
{
    /// <summary>
    /// Runtime-safe (build + editor) GZip+Base64 codec for <see cref="LevelData"/> JSON. Shared by the
    /// Level Editor Window copy/paste tool (<see cref="LevelDataUtilities"/>) and runtime cheats (e.g.
    /// copy-current-level) so both produce/consume the exact same clipboard string format.
    /// </summary>
    public static class LevelDataSerializationUtilities
    {
        public static string ToCompressedString(LevelData data)
        {
            string json = JsonUtility.ToJson(data, false);
            return CompressToBase64(json);
        }

        /// <summary>Accepts either raw level JSON or a GZip+Base64 compressed payload.</summary>
        public static bool TryApplyString(string text, LevelData target, out string error)
        {
            error = null;
            if (!target)
            {
                error = "No level asset.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Input is empty.";
                return false;
            }

            string json;
            string trimmed = text.Trim();
            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                json = trimmed;
            }
            else
            {
                if (!TryDecompressFromBase64(trimmed, out json))
                {
                    error = "Input does not look like level JSON or compressed level data.";
                    return false;
                }

                if (!json.StartsWith("{", StringComparison.Ordinal))
                {
                    error = "Decompressed data does not look like level JSON.";
                    return false;
                }
            }

            JsonUtility.FromJsonOverwrite(json, target);
            target.Validate();
            return true;
        }

        private static string CompressToBase64(string text)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(text);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
                gzip.Write(inputBytes, 0, inputBytes.Length);
            return Convert.ToBase64String(output.ToArray());
        }

        private static bool TryDecompressFromBase64(string base64, out string result)
        {
            result = null;
            try
            {
                byte[] compressed = Convert.FromBase64String(base64);
                using var input = new MemoryStream(compressed);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                result = Encoding.UTF8.GetString(output.ToArray());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
