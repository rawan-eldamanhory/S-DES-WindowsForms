using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private byte[] sharedKey;

        // S-DES Constants
        private static readonly int[] IP = { 1, 5, 2, 0, 3, 7, 4, 6 };
        private static readonly int[] FP = { 3, 0, 2, 4, 6, 1, 7, 5 };
        private static readonly int[] EP = { 3, 0, 1, 2, 1, 2, 3, 0 };
        private static readonly int[,] S0 = {
            { 1, 0, 3, 2 },
            { 3, 2, 1, 0 },
            { 0, 2, 1, 3 },
            { 3, 1, 3, 2 }
        };
        private static readonly int[,] S1 = {
            { 0, 1, 2, 3 },
            { 2, 0, 1, 3 },
            { 3, 0, 1, 0 },
            { 2, 1, 0, 3 }
        };
        private static readonly int[] P4 = { 1, 3, 2, 0 };

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Step 1: Perform the Diffie-Hellman key exchange protocol
            using (var dh = new ECDiffieHellmanCng())
            {
                dh.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
                dh.HashAlgorithm = CngAlgorithm.Sha256;

                // Generate your private key
                byte[] privateKey = dh.Key.Export(CngKeyBlobFormat.EccPrivateBlob);

                // Step 2: Share your public key with the other party
                byte[] publicKey = dh.PublicKey.ToByteArray();

                // Step 3: Use your public key as the other party's public key (self-loop)
                byte[] otherPartyPublicKey = publicKey;

                // Step 4: Derive the shared secret key
                sharedKey = dh.DeriveKeyMaterial(CngKey.Import(otherPartyPublicKey, CngKeyBlobFormat.EccPublicBlob));

                // The sharedKey is now ready for encryption and decryption
            }
        }

        private void BtnEncrypt_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPlaintext.Text))
            {
                MessageBox.Show("Please enter the plaintext to encrypt.");
                return;
            }

            string plaintext = txtPlaintext.Text;

            // Step 5: Encrypt the plaintext
            string ciphertext = EncryptText(plaintext, sharedKey);

            // Display the ciphertext
            txtEncrypted.Text = ciphertext;
        }

        private void BtnDecrypt_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtEncrypted.Text))
            {
                MessageBox.Show("Please enter the ciphertext to decrypt.");
                return;
            }

            string ciphertext = txtEncrypted.Text;

            // Step 6: Decrypt the ciphertext
            string decryptedText = DecryptText(ciphertext, sharedKey);

            // Display the decrypted text
            txtDecrypted.Text = decryptedText;
        }

        private string EncryptText(string plainText, byte[] key)
        {
            byte[] plaintextBytes = Encoding.ASCII.GetBytes(plainText);
            byte[] encryptedBytes = new byte[plaintextBytes.Length];

            for (int i = 0; i < plaintextBytes.Length; i++)
            {
                byte encryptedByte = EncryptByte(plaintextBytes[i], key);
                encryptedBytes[i] = encryptedByte;
            }

            return Convert.ToBase64String(encryptedBytes);
        }

        private string DecryptText(string ciphertext, byte[] key)
        {
            byte[] encryptedBytes = Convert.FromBase64String(ciphertext);
            byte[] decryptedBytes = new byte[encryptedBytes.Length];

            for (int i = 0; i < encryptedBytes.Length; i++)
            {
                byte decryptedByte = DecryptByte(encryptedBytes[i], key);
                decryptedBytes[i] = decryptedByte;
            }

            return Encoding.ASCII.GetString(decryptedBytes);
        }

        private byte EncryptByte(byte plaintextByte, byte[] key)
        {
            byte[] subkeys = GenerateSubkeys(key);

            byte initialPermutation = Permute(plaintextByte, IP);

            byte fkResult = ApplyFk(initialPermutation, subkeys[0]);
            byte middleStep = ((byte)(fkResult << 4 | fkResult >> 4));

            byte swResult = ApplySw(middleStep);

            byte encryptedByte = Permute(swResult, FP);

            return encryptedByte;
        }

        private byte DecryptByte(byte encryptedByte, byte[] key)
        {
            byte[] subkeys = GenerateSubkeys(key);

            byte initialPermutation = Permute(encryptedByte, IP);

            byte fkResult = ApplyFk(initialPermutation, subkeys[1]);

            byte middleStep = ((byte)(fkResult << 4 | fkResult >> 4));

            byte swResult = ApplySw(middleStep);

            byte decryptedByte = Permute(swResult, FP);

            return decryptedByte;
        }

        private byte ApplyFk(byte value, byte subkey)
        {
            byte expandedValue = Permute(value, EP);

            byte xorResult = ((byte)(expandedValue ^ subkey));

            byte left4bits = (byte)(xorResult >> 4);
            byte right4bits = (byte)(xorResult & 0x0F);

            byte s0Result = (byte)S0[left4bits >> 2, left4bits & 0x03];
            byte s1Result = (byte)S1[right4bits >> 2, right4bits & 0x03];

            byte mergedResult = (byte)(s0Result << 2 | s1Result);

            byte p4Result = Permute(mergedResult, P4);

            byte fkResult = ((byte)(value ^ p4Result));

            return fkResult;
        }

        private byte ApplySw(byte value)
        {
            byte leftHalf = (byte)(value >> 4);
            byte rightHalf = (byte)(value & 0x0F);

            byte swResult = (byte)(rightHalf << 4 | leftHalf);

            return swResult;
        }

        private byte[] GenerateSubkeys(byte[] key)
        {
            byte[] subkeys = new byte[2];

            byte p10Result = Permute(key[0], new int[] { 2, 4, 1, 6, 3, 9, 0, 8, 7, 5 });
            byte p8Result = Permute(p10Result, new int[] { 5, 2, 6, 3, 7, 4, 9, 8 });

            subkeys[0] = p8Result;

            byte leftShiftedKey = (byte)((key[0] << 1) | (key[1] >> 3));
            byte p10Result2 = Permute(leftShiftedKey, new int[] { 2, 4, 1, 6, 3, 9, 0, 8, 7, 5 });
            byte p8Result2 = Permute(p10Result2, new int[] { 5, 2, 6, 3, 7, 4, 9, 8 });

            subkeys[1] = p8Result2;

            return subkeys;
        }

        private byte Permute(byte value, int[] permutation)
        {
            byte result = 0;

            for (int i = 0; i < permutation.Length; i++)
            {
                int bitPosition = permutation[i];
                byte bitValue = (byte)((value >> (7 - bitPosition)) & 0x01);
                result |= (byte)(bitValue << (permutation.Length - 1 - i));
            }

            return result;
        }
    }
}
