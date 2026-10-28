using VocalEye.Desktop.Core.Models;
using Xunit;
namespace VocalEye.Desktop.UnitTests;
public sealed class BasicTests { [Fact] public void BackendInfo_HasSafeDefaults() { var info=new BackendInfo(); Assert.Equal("VocalEye API",info.Name); Assert.Equal("Unknown",info.Version); Assert.Equal("Unknown",info.Environment); } }
