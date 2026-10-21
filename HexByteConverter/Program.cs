using System;

namespace HexByteConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            // The input hex bytes string
            string hexBytes = "0000000F5449445F4441494C595F4749465453000000000300000000001151756573745F4672656547696674733130000000145449445F465245455F43484553545F51554553540000000873632F75692E73630000001571756573745F6974656D5F667265655F636865737405031307011307010501900103000000030004040000020100000000155449445F4C41444445525F51554553545F504C4159000000000A01000000001A506C61795F51756573745F506C61795F4C61646465725F5076500000001A5449445F4C41444445525F51554553545F504C41595F494E464F0000000873632F75692E73630000000E71756573745F6974656D5F70767014010E000A0000010100000032010000BF060201000301000000000000000000000000000000";
            // Call the method to convert and format the string
            string spacedOutHex = ConvertHexToSpacedFormat(hexBytes);

            // Print the result
            Console.WriteLine(spacedOutHex);

            // Keep the console window open
            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        /// <summary>
        /// Converts a string of hex bytes to a format where each byte is spaced out with a dash.
        /// </summary>
        /// <param name="hex">The string of hex bytes to format.</param>
        /// <returns>A formatted string with each byte separated by a dash.</returns>
        static string ConvertHexToSpacedFormat(string hex)
        {
            // Split the hex string into pairs of characters (each pair represents a byte)
            var bytes = new System.Text.StringBuilder();

            for (int i = 0; i < hex.Length; i += 2)
            {
                // Append each byte and a dash, except for the last byte
                bytes.Append(hex.Substring(i, 2));
                if (i + 2 < hex.Length)
                {
                    bytes.Append("-");
                }
            }

            return bytes.ToString();
        }
    }
}


