using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VK_CMSIS_SVD_Test
{
    [TestClass]
    public class UnitTest
    {
        [TestMethod]
        public void TestMethod1()
        {
            XmlSerializer xs = new XmlSerializer(typeof(cmsis_svd.device));
            StreamReader reader = new StreamReader("svd_Example_pg.xml");
            cmsis_svd.device device = (cmsis_svd.device) xs.Deserialize(reader);
        }
    }
}
