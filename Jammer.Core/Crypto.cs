using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32.SafeHandles;
using NSec.Cryptography;
using PublicKey = NSec.Cryptography.PublicKey;


namespace Jammer.Core;

public class Crypto
{
    public static class AES
    {
        private const int nonceSize= 12;
        private const int tagSize = 16;
        
        
        
        public static byte[] Encrypt(string data, byte[] key)
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

        public static byte[] Decrypt(byte[] encryptedData, byte[] key)
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

    public class ECDH
    {
        private KeyAgreementAlgorithm _algorithm = KeyAgreementAlgorithm.X25519;
        
        private Key _privateKey;
    
        private PublicKey _publicKey;

        private void KeyGeneration()
        {
            KeyCreationParameters keyCreationParameters = new KeyCreationParameters
                { ExportPolicy = KeyExportPolicies.AllowPlaintextExport };
            
            _privateKey = Key.Create(_algorithm, keyCreationParameters);
            _publicKey = _privateKey.PublicKey;
        }
        
        private byte[] GetPublicKeyBytes()
        {
            if (_publicKey == null)
            {
                throw new InvalidOperationException("Критическая ошибка: публичный ключ отсутствует или не сгенерирован");
            }
            
            byte[] publicKeyBytes = _publicKey.Export(KeyBlobFormat.RawPublicKey);
            
            return publicKeyBytes; 
        }
        
        
        private PublicKey ImportPublicKey(byte[] publicKeyBytes)
        {
            if (publicKeyBytes == null || publicKeyBytes.Length!=32)
            {
                int actualLength = publicKeyBytes?.Length ?? 0;
                throw new ArgumentNullException($"Критическая ошибка: публичный ключ отсутствует или неверной длины {actualLength}");
            }
            
            try
            {
                return PublicKey.Import(_algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
            }
            catch (Exception ex)
            {
                
                throw new InvalidOperationException("Критическая ошибка: не удалось импортировать ключ", ex);
            }
        }

        

        public byte[] CreateSecret(PublicKey publicKey)
        {
            if (_privateKey==null || publicKey==null)
            {
                throw new ArgumentNullException(
                    "Критическая ошибка: не удалось создать общий секрет, приватный или публичный ключи отсутствуют");
            }

            using (SharedSecret secret = _algorithm.Agree(_privateKey, publicKey))
            {
                
                byte[] salt = Array.Empty<byte>(); 
                byte[] info = System.Text.Encoding.UTF8.GetBytes("Chating_AES_256_Key");
                
                byte[] aesKeyBytes = KeyDerivationAlgorithm.HkdfSha256.DeriveBytes(secret, salt, info, 32);
        
                return aesKeyBytes;
            }
        }

        public async Task SendLocalPublickeyAsync(Socket client)
        {
            if (client == null)
            {
                throw new ArgumentNullException("[ECDH] передан пустой сокет client");
            }

            if (!client.Connected)
            {
                throw new InvalidOperationException("[ECDH] Соединение не установлено");
            }

            try
            {
                KeyGeneration();
                byte[] buffer= GetPublicKeyBytes();
                await client.SendAsync(buffer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ECDH] {ex}");
                throw;
            }
        }

        public async Task<PublicKey> ReceiveRemotePublicKeyAsync(Socket client)
        {
            if (client==null)
            {
                throw new ArgumentNullException("[ECDH] передан пустой сокет client");
            }

            if (!client.Connected)
            {
                throw new InvalidOperationException("[ECDH] Соединение не установлено");
            }

            byte[] buffer = new byte[32];
            try
            {
                await client.ReceiveAsync(buffer, SocketFlags.None);
                return ImportPublicKey(buffer);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ECDH] {ex}");
                throw;
            }
        }
    }
}