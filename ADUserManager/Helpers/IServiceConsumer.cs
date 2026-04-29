using ActiveManager.Services;
using ActiveManager.Views;

namespace ActiveManager.Helpers;

public interface IServiceConsumer
{
    void SetServices(UserService? adService, ITerminationService? terminationService, DatabaseService databaseService, MainWindow mainWindow);
}
