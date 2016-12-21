using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Windows;
using ConsoleApplication2.ServiceReference1;

namespace ConsoleApplication2
{
    class Methods
    {

       
        AlertCompleteListResult alerts;
        bool tracedb = false;

        private static byte[] Key = { 23, 85, 1, 6, 46, 156, 123, 72 };
        private static byte[] IV = { 87, 143, 245, 8, 9, 19, 26, 73 };

        public static String Encrypt(string pass)
        {
            if (string.IsNullOrWhiteSpace(pass)) return String.Empty;
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            ICryptoTransform crypt = des.CreateEncryptor(Key, IV);
            byte[] b = UTF8Encoding.UTF8.GetBytes(pass);
            return Convert.ToBase64String(crypt.TransformFinalBlock(b, 0, b.Length));
        }
        public static string Decrypt(string pass)
        {
            if (string.IsNullOrWhiteSpace(pass)) return null;
            try
            {
                DESCryptoServiceProvider des = new DESCryptoServiceProvider();
                ICryptoTransform decrypt = des.CreateDecryptor(Key, IV);
                byte[] b = Convert.FromBase64String(pass);
                return UTF8Encoding.UTF8.GetString(decrypt.TransformFinalBlock(b, 0, b.Length));
            }
            catch (Exception)
            {
                return null;
            }
        }



        


    }
}
