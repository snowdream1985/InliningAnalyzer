﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Model.Samples
{
    class RefReturnMethods
    {
        private static int a;
        private static int b;

        private static ref int Select(bool which)
            => ref (which ? ref a : ref b);

        private static int Select2(bool which)
            => Select(which);

        public static void Main(string[] args)
        {
            ref int x = ref Select(true);
            int y = Select(true);
            int z = Select2(true);
            Console.WriteLine(x);
            Console.WriteLine(y);
        }
    }

    public class RefMethods
    {
        private static int a;
        private static int b;

        private static ref int Select(ref bool which)
            => ref (which ? ref a : ref b);

        private static int Select2(ref bool which)
            => Select(ref which);

        public static void Main(string[] args)
        {
            bool which = true;
            ref int x = ref Select(ref which);
            int y = Select(ref which);
            int z = Select2(ref which);
            Console.WriteLine(x);
            Console.WriteLine(y);
            Console.WriteLine(z);
        }
    }

}
