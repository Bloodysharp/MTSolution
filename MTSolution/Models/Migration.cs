using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTSolution.Models
{
    class Migration
    {
        public string Vm { get; }
        public string From { get; }
        public string To { get; }

        public Migration(string vm, string from, string to)
        {
            Vm = vm;
            From = from;
            To = to;
        }
    }
}
