namespace delivery_system_dotnet.Interfaces;

public interface IOrderManager
{
    int ChangeStatus(string orderId, string userRole, string newStatus);
}
