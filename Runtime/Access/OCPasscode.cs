#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace OperatorConsole
{
    /// <summary>The numeric Passcode of a Panel Lock, kept only as a salted SHA-256 hash.</summary>
    [Serializable]
    public sealed class OCPasscode
    {
        public const int MinLength = 4;
        public const int MaxLength = 8;

        [SerializeField] private int _length;
        [SerializeField] private string _salt = string.Empty;
        [SerializeField] private string _hash = string.Empty;

        public bool IsSet => _length > 0 && _hash.Length > 0;

        /// <summary>Digit count of the Passcode; 0 when none is set.</summary>
        public int Length => IsSet ? _length : 0;

        public static bool IsValid(string? code)
        {
            if (code == null || code.Length < MinLength || code.Length > MaxLength) return false;
            foreach (var c in code)
                if (c < '0' || c > '9') return false;
            return true;
        }

        /// <exception cref="ArgumentException">The code is not 4–8 digits.</exception>
        public static OCPasscode Create(string code)
        {
            var passcode = new OCPasscode();
            passcode.Set(code);
            return passcode;
        }

        /// <exception cref="ArgumentException">The code is not 4–8 digits.</exception>
        public void Set(string code)
        {
            if (!IsValid(code)) throw new ArgumentException($"A Passcode is {MinLength}–{MaxLength} digits.", nameof(code));
            _salt = NewSalt();
            _hash = Hash(code, _salt);
            _length = code.Length;
        }

        public void Clear()
        {
            _length = 0;
            _salt = string.Empty;
            _hash = string.Empty;
        }

        public bool Matches(string? code)
        {
            if (!IsSet || code == null || code.Length != _length) return false;
            return FixedTimeEquals(Hash(code, _salt), _hash);
        }

        internal static string NewSalt()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        internal static string Hash(string code, string salt)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(salt + ":" + code)));
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
