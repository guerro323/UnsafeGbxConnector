namespace UnsafeGbxConnector.Serialization
{
    public readonly struct GbxError
    {
        public readonly int Code;
        public readonly string Text;

        public bool IsError => Code > 0;

        public GbxError(int code, string text)
        {
            Code = code;
            Text = text;
        }

        public static GbxError None => new(0, string.Empty);

        public override string ToString()
        {
            return $"Error({Code}: {Text})";
        }
    }
}