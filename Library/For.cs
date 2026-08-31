namespace XQuinn
{
    public static class For
    {
        public static bool NeedsDelimiter(int length, int i)
        {
            return length > 1 && i < length - 1;
        }
    }
}