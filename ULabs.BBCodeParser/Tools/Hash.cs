using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace ULabs.BBCodeParser.Tools {
    public class Hash {
        public static string Md5(string inputStr, bool toUpper = false) {
            MD5 md5 = MD5.Create();
            byte[] inputData = Encoding.UTF8.GetBytes(inputStr);
            byte[] rawHash = md5.ComputeHash(inputData);

            StringBuilder sb = new StringBuilder();
            string format = toUpper ? "X2" : "x2";
            for (int i = 0; i < rawHash.Length; i++) {
                sb.Append(rawHash[i].ToString(format));
            }
            string hash = sb.ToString();
            return hash;
        }
    }
}
