using Xunit;
using Elatec.NET.Cards.Mifare;

namespace Elatec.NET.Tests
{
    public class AccessRightsTests
    {
        [Fact]
        public void ToAccessRightsWord_PacksInDatasheetOrder()
        {
            var accessRights = new DESFireFileAccessRights
            {
                ReadKeyNo = 0x1,
                WriteKeyNo = 0x2,
                ReadWriteKeyNo = 0x3,
                ChangeKeyNo = 0x4
            };

            Assert.Equal((ushort)0x1234, accessRights.ToAccessRightsWord());
        }

        [Fact]
        public void FromAccessRightsWord_UnpacksInDatasheetOrder()
        {
            var accessRights = DESFireFileAccessRights.FromAccessRightsWord(0xABCD);

            Assert.Equal(0xA, accessRights.ReadKeyNo);
            Assert.Equal(0xB, accessRights.WriteKeyNo);
            Assert.Equal(0xC, accessRights.ReadWriteKeyNo);
            Assert.Equal(0xD, accessRights.ChangeKeyNo);
        }
    }
}
