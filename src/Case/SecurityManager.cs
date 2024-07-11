//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Case
{
    public class SecurityManager
    {
        public static GlobalParameters gP = new GlobalParameters();
        private static string passwordFilePath = gP.getSetting("SFile");

        /// <summary>
        /// Prompts the user to create a new password and stores it in a file.
        /// </summary>
        public static void CreatePasswordFile()
        {
            while (true)
            {
                Console.WriteLine("Enter a new password (at least 8 characters, including one uppercase, one lowercase, one digit, and one special character) or type 'cancel' to exit:");
                string password = Console.ReadLine();

                if (password.ToLower() == "cancel")
                {
                    Console.WriteLine("Password creation canceled.");
                    return;
                }
                if (!IsValidPassword(password))
                {
                    Console.WriteLine("Password does not meet requirements.");
                    continue;
                }
                else
                {
                    try
                    {
                        byte[] salt;
                        string hash = GenerateKey(password, out salt);
                        byte[] saltAndHash = new byte[salt.Length + Convert.FromBase64String(hash).Length];
                        Array.Copy(salt, 0, saltAndHash, 0, salt.Length);
                        Array.Copy(Convert.FromBase64String(hash), 0, saltAndHash, salt.Length, Convert.FromBase64String(hash).Length);

                        File.WriteAllBytes(passwordFilePath, saltAndHash);
                        Console.WriteLine("Password file created successfully.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating password file: {ex.Message}");
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Validates the password based on given criteria.
        /// </summary>
        /// <param name="password">The password to validate.</param>
        /// <returns>True if the password meets the requirements, otherwise false.</returns>
        private static bool IsValidPassword(string password)
        {
            var regex = new Regex(@"^(?=.*[A-Z])(?=.*[!@#$&#%$^*]).{8,}$");
            return regex.IsMatch(password);
        }

        /// <summary>
        /// Checks if the password file exists.
        /// </summary>
        /// <returns>True if the password file exists, otherwise false.</returns>
        public static bool IsPasswordConfigured()
        {
            try
            {
                return File.Exists(passwordFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking password file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Prompts the user to enter their password and verifies it against the stored password.
        /// </summary>
        /// <returns>True if the password is verified successfully, otherwise false.</returns>
        public static bool CheckPassword()
        {
            while (true)
            {
                Console.WriteLine("Enter your password or type 'cancel' to exit:");
                string password = Console.ReadLine();

                if (password.ToLower() == "cancel")
                {
                    Console.WriteLine("Password check canceled.");
                    return false;
                }

                try
                {
                    if (File.Exists(passwordFilePath))
                    {
                        byte[] saltAndHash = File.ReadAllBytes(passwordFilePath);
                        byte[] salt = new byte[16];
                        byte[] storedHash = new byte[saltAndHash.Length - 16];
                        Array.Copy(saltAndHash, 0, salt, 0, 16);
                        Array.Copy(saltAndHash, 16, storedHash, 0, storedHash.Length);

                        string inputHash = HashPBKDF2(password, salt, 10000);
                        if (Convert.ToBase64String(storedHash) == inputHash)
                        {
                            Console.WriteLine("Password verified successfully.");
                            return true;
                        }
                        else
                        {
                            Console.WriteLine("Incorrect password.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Password file not found.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during password verification: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Prompts the user to change their password after verifying the current password.
        /// </summary>
        public static void ChangePassword()
        {
            if (CheckPassword())
            {
                while (true)
                {
                    Console.WriteLine("Enter a new password (at least 8 characters, including one uppercase, one lowercase, one digit, and one special character) or type 'cancel' to exit:");
                    string newPassword = Console.ReadLine();

                    if (newPassword.ToLower() == "cancel")
                    {
                        Console.WriteLine("Password change canceled.");
                        return;
                    }

                    if (!IsValidPassword(newPassword))
                    {
                        Console.WriteLine("New password does not meet requirements.");
                        continue;
                    }
                    else
                    {
                        try
                        {
                            byte[] salt;
                            string newHash = GenerateKey(newPassword, out salt);
                            byte[] saltAndHash = new byte[salt.Length + Convert.FromBase64String(newHash).Length];
                            Array.Copy(salt, 0, saltAndHash, 0, salt.Length);
                            Array.Copy(Convert.FromBase64String(newHash), 0, saltAndHash, salt.Length, Convert.FromBase64String(newHash).Length);

                            File.WriteAllBytes(passwordFilePath, saltAndHash);
                            Console.WriteLine("Password changed successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error changing password: {ex.Message}");
                        }
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Generates a secure hash for the given input.
        /// </summary>
        /// <param name="input">The input string (password) to hash.</param>
        /// <param name="salt">The generated salt for the hash.</param>
        /// <returns>The hashed password as a base64 string.</returns>
        private static string GenerateKey(string input, out byte[] salt)
        {
            salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);  // Generates a secure random salt
            }
            return HashPBKDF2(input, salt, 10000);
        }

        /// <summary>
        /// Hashes a password using PBKDF2 with the provided salt and iterations.
        /// </summary>
        /// <param name="password">The password to hash.</param>
        /// <param name="salt">The salt to use in the hashing process.</param>
        /// <param name="iterations">The number of iterations for the PBKDF2 algorithm.</param>
        /// <returns>The hashed password as a base64 string.</returns>
        private static string HashPBKDF2(string password, byte[] salt, int iterations)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                byte[] hash = pbkdf2.GetBytes(20);
                return Convert.ToBase64String(hash);
            }
        }
    }

}
