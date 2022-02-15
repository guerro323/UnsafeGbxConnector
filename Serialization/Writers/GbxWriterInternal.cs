using System.Text;

namespace UnsafeGbxConnector.Serialization.Writers
{
    internal static class GbxWriterInternal
    {
        public static readonly string BeginWrite = Begin();
        public static readonly string EndWrite = End();
        public static readonly string EndToMessageWrite = EndToMessage();

        private static string Begin()
        {
            var sb = new StringBuilder();
            sb.Append("<member>");
            {
                sb.Append("<name>");
                {
                    sb.Append("methodName");
                }
                sb.Append("</name>");

                sb.Append("<value>");
                {
                    sb.Append("<string>");
                    {
                        // METHOD NAME
                    }
                }
            }
            // END WRITE

            return sb.ToString();
        }

        private static string End()
        {
            var sb = new StringBuilder();
            // BEGIN WRITE
            {
                // METHOD NAME
                {
                    sb.Append("</string>");
                }
                sb.Append("</value>");
            }
            sb.Append("</member>");

            sb.Append("<member>");
            sb.Append("<name>");
            {
                sb.Append("params");
            }
            sb.Append("</name>");
            sb.Append("<value>");
            sb.Append("<array>");
            sb.Append("<data>");

            return sb.ToString();
        }

        private static string EndToMessage()
        {
            var sb = new StringBuilder();
            sb.Append("</data>");
            sb.Append("</array>");
            sb.Append("</value>");
            sb.Append("</member>");
            return sb.ToString();
        }
    }
}