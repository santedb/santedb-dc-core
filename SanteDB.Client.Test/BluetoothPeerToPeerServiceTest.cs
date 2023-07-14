using NUnit.Framework;
using SanteDB.Core.TestFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Client.Test
{
    [TestFixture]
    public class BluetoothPeerToPeerServiceTest
    {
        [OneTimeSetUp]
        public void Initialize()
        {
            // Force load of the DLL
            TestApplicationContext.TestAssembly = typeof(TestPeerToPeerSerialization).Assembly;
            TestApplicationContext.Initialize(TestContext.CurrentContext.TestDirectory);
        }

    }
}
