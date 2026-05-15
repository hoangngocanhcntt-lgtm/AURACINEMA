using System;

namespace AuraCinema.Domain.Helpers
{
    public static class CodeGenerator
    {
        public static string Generate(string prefix)
        {
            string datePart = DateTime.Now.ToString("yyMMdd");
            string randomPart = Guid.NewGuid().ToString().Substring(0, 4).ToUpper();
            return $"{prefix}-{datePart}-{randomPart}";
        }

        public static string GenerateMovieCode() => Generate("MOV");
        public static string GenerateRoomCode() => Generate("ROOM");
        public static string GenerateShowtimeCode() => Generate("ST");
        public static string GeneratePromoCode() => Generate("PRM");
        public static string GenerateServiceCode() => Generate("SVC");
        public static string GenerateUserCode() => Generate("USR");
        public static string GenerateOrderCode() => Generate("ORD");
    }
}
