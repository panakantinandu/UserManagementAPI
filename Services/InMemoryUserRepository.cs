using System.Collections.Concurrent;
using UserManagementAPI.Models;

namespace UserManagementAPI.Services;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<int, User> _users = new();
    private int _nextId;

    public InMemoryUserRepository()
    {
        Add(new User { FirstName = "Jane", LastName = "Doe", Email = "jane.doe@example.com" });
        Add(new User { FirstName = "John", LastName = "Smith", Email = "john.smith@example.com" });
    }

    public IEnumerable<User> GetAll() => _users.Values.OrderBy(u => u.Id);

    public User? GetById(int id) => _users.TryGetValue(id, out var user) ? user : null;

    public User Add(User user)
    {
        user.Id = Interlocked.Increment(ref _nextId);
        _users[user.Id] = user;
        return user;
    }

    public bool Update(int id, User user)
    {
        if (!_users.ContainsKey(id))
        {
            return false;
        }

        user.Id = id;
        _users[id] = user;
        return true;
    }

    public bool Delete(int id) => _users.TryRemove(id, out _);
}
