using System.IO;

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

		public static string FormatLogMessage(string message)
		{
			return $"<b>Editor Screenshot:</b> {message}";
		}

		public static uint GetGreatestCommonDivisor(uint a, uint b)
		{
			while (a != 0 && b != 0)
			{
				if (a > b)
					a %= b;
				else
					b %= a;
			}

			return a | b;
		}
	}
}