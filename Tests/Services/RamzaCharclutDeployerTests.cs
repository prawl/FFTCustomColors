using Xunit;
using FFTColorCustomizer.Services;

namespace Tests.Services
{
    public class RamzaCharclutDeployerTests
    {
        [Fact]
        public void Constructor_ShouldAcceptGameDataPath()
        {
            // Arrange & Act
            var deployer = new RamzaCharclutDeployer(@"C:\game\FFTIVC\data");

            // Assert
            Assert.NotNull(deployer);
        }

        [Fact]
        public void GetDeploymentPath_ShouldReturnCorrectNxdPath()
        {
            // Arrange
            var deployer = new RamzaCharclutDeployer(@"C:\game\FFTIVC\data");

            // Act
            var path = deployer.GetDeploymentPath();

            // Assert
            Assert.Equal(@"C:\game\FFTIVC\data\enhanced\nxd\charclut.nxd", path);
        }

        [Fact]
        public void GetBackupPath_ShouldReturnPathWithBackupExtension()
        {
            // Arrange
            var deployer = new RamzaCharclutDeployer(@"C:\game\FFTIVC\data");

            // Act
            var path = deployer.GetBackupPath();

            // Assert
            Assert.Equal(@"C:\game\FFTIVC\data\enhanced\nxd\charclut.nxd.backup", path);
        }

        [Fact]
        public void GetNxdDirectory_ShouldReturnCorrectDirectory()
        {
            // Arrange
            var deployer = new RamzaCharclutDeployer(@"C:\game\FFTIVC\data");

            // Act
            var path = deployer.GetNxdDirectory();

            // Assert
            Assert.Equal(@"C:\game\FFTIVC\data\enhanced\nxd", path);
        }

        [Fact]
        public void Deploy_WithNonExistentSourceFile_ShouldReturnFalse()
        {
            // Arrange
            var deployer = new RamzaCharclutDeployer(@"C:\game\FFTIVC\data");

            // Act
            var result = deployer.Deploy(@"C:\nonexistent\charclut.nxd");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Undeploy_WhenNoDeployedFile_ShouldReturnTrue()
        {
            // Arrange
            var deployer = new RamzaCharclutDeployer(@"C:\game\FFTIVC\data");

            // Act - undeploy when nothing is deployed should succeed (no-op)
            var result = deployer.Undeploy();

            // Assert
            Assert.True(result);
        }
    }
}
