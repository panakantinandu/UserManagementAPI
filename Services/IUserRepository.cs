using UserManagementAPI.Models;

namespace UserManagementAPI.Services;

public interface IUserRepository
{
    IEnumerable<User> GetAll();

    User? GetById(int id);

    User Add(User user);

    bool Update(int id, User user);

    bool Delete(int id);

    bool EmailExists(string email, int? excludeUserId = null);
}
