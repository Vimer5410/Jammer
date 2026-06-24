using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Jammer.Core;

public class Crypto
{
    public static class AES
    {
        private const int nonceSize= 12;
        private const int tagSize = 16;
        
        private static readonly byte[] key = new byte[32] { 
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 
        };
        
        
        
        
        public static byte[] Encrypt(string data)
        {
            byte[] nonce = new byte[nonceSize];
            byte[] tag = new byte[tagSize];
            
            RandomNumberGenerator.Fill(nonce);
            
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] encryptedData = new byte[dataBytes.Length];
            
            using (AesGcm _aesGcm = new AesGcm(key,tagSizeInBytes:16))
            {
                _aesGcm.Encrypt(nonce, dataBytes, encryptedData, tag);
            }
            
            var finalPackage = nonce.Concat(encryptedData).Concat(tag).ToArray();
            
            return finalPackage;
        }

        public static byte[] Decrypt(byte[] encryptedData)
        {
            
            int encryptedDataLenght = encryptedData.Length - nonceSize - tagSize;
            
            byte[] nonce = encryptedData.Take(nonceSize).ToArray();
            byte[] cipherText = encryptedData.Skip(nonceSize).Take(encryptedDataLenght).ToArray();
            byte[] tag = encryptedData.Skip(nonceSize + encryptedDataLenght).Take(tagSize).ToArray();
            
            byte[] decryptedData = new byte[encryptedDataLenght];
            
            using (AesGcm _aesGcm = new AesGcm(key, tagSizeInBytes:16))
            {
                _aesGcm.Decrypt(nonce, cipherText, tag, decryptedData);
            }
            
            return decryptedData;
        }
        
        
    }
}