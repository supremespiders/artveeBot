using System;

namespace artveeBot.Models
{
    public class KnownException : Exception
    {
        public KnownException(string s) : base(s)
        {

        }
    }
}