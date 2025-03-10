using System.IO;
using System.Linq;
using UnityEngine;

namespace Pumkin.EditorScreenshot
{
	internal static class EditorScreenshotUtility
	{
		public static string GetUniqueFileName(string fileName, string folderPath)
		{
			string pathAndFileName = Path.Combine(folderPath, fileName);
			string validatedName = fileName;
			string fileNameWithoutExt = Path.GetFileNameWithoutExtension(pathAndFileName);
			string ext = Path.GetExtension(pathAndFileName);
			int count = 1;
			while (File.Exists(Path.Combine(folderPath, validatedName)))
			{
				validatedName = string.Format("{0}_{1}{2}", fileNameWithoutExt, count++, ext);
			}
			return validatedName;
		}

		/// <summary>
		/// Calculates the scale factor needed to fit dimensions within maximum bounds while preserving aspect ratio.
		/// </summary>
		/// <param name="width">The current width value</param>
		/// <param name="height">The current height value</param>
		/// <param name="maxWidth">The maximum allowed width</param>
		/// <param name="maxHeight">The maximum allowed height</param>
		/// <returns>A scale factor (0.0-1.0) to apply to both dimensions. Returns 1.0 if no scaling is needed.</returns>
		public static float GetScaleFactor(int width, int height, int maxWidth, int maxHeight)
		{
			float widthScale = 1f;
			float heightScale = 1f;
    
			if (width > maxWidth)
				widthScale = (float)maxWidth / width;
        
			if (height > maxHeight)
				heightScale = (float)maxHeight / height;
    
			return Mathf.Min(widthScale, heightScale);
		}

		/// <summary>
		/// Clamps resolution to width and height and returns true if resolution was clamped
		/// </summary>
		/// <param name="width">Width to clamp</param>
		/// <param name="height">Height to clamp</param>
		/// <param name="maxWidth">Width to clamp to</param>
		/// <param name="maxHeight">Height to clamp to</param>
		/// <returns>True if resolution was clamped</returns>
		public static bool ClampResolution(ref int width, ref int height, int maxWidth, int maxHeight)
		{
			float scale = GetScaleFactor(width, height, maxWidth, maxHeight);
    
			if (scale < 1f)
			{
				width = (int)Mathf.Round(width * scale);
				height = (int)Mathf.Round(height * scale);
				return true;
			}

			return false;
		}
	}
}