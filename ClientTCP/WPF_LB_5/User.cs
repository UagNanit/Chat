using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace WPF_LB_5
{
    [Serializable]
    public class User
    {
        public User(string name, string pas)
        {
            Name = name;
            Password = pas;
        }
        public string Name { get; private set; }
        public string Password { get; private set; }
    }
}
