using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace MachineLearning.Soccer
{
    public enum SoccerTacticType
    {
        Base,
        Attack,
        Defense,
        Press
    }

    /// <summary>
    /// ONNX 파일명 규칙과 HUD 전술명 해석 기준.
    /// </summary>
    public static class SoccerModelNaming
    {
        public const string FileNamePattern = "Type-YYYYMMDD-vNNN.onnx";

        static readonly Regex ModelNameRegex = new(
            "^(Base|Attack|Defense|Press)-(?<date>\\d{8})-v(?<version>\\d{3})$",
            RegexOptions.CultureInvariant);

        public static bool TryParse(
            string assetName,
            out SoccerTacticType tactic,
            out DateTime trainedDate,
            out int version)
        {
            tactic = default;
            trainedDate = default;
            version = 0;
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return false;
            }

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(assetName);
            var match = ModelNameRegex.Match(nameWithoutExtension);
            return match.Success
                && Enum.TryParse(match.Groups[1].Value, false, out tactic)
                && DateTime.TryParseExact(
                    match.Groups["date"].Value,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out trainedDate)
                && int.TryParse(match.Groups["version"].Value, out version)
                && version > 0;
        }

        public static string GetKoreanName(SoccerTacticType tactic)
        {
            return tactic switch
            {
                SoccerTacticType.Attack => "공격형",
                SoccerTacticType.Defense => "수비형",
                SoccerTacticType.Press => "압박형",
                _ => "기본형"
            };
        }
    }
}
