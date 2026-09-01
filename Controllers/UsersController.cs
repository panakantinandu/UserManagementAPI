using Microsoft.AspNetCore.Mvc;
using UserManagementAPI.Models;
using UserManagementAPI.Services;

namespace UserManagementAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _repository;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserRepository repository, ILogger<UsersController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // GET: api/users
    // GET: api/users?page=1&pageSize=20
    [HttpGet]
    public ActionResult<IEnumerable<User>> GetUsers([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var users = _repository.GetAll();

        if (page is > 0 && pageSize is > 0)
        {
            users = users.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value);
        }

        return Ok(users);
    }

    // GET: api/users/5
    [HttpGet("{id:int}")]
    public ActionResult<User> GetUser(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new { error = "Id must be a positive integer." });
        }

        var user = _repository.GetById(id);
        if (user is null)
        {
            _logger.LogWarning("GetUser: no user found with id {UserId}", id);
            return NotFound(new { error = $"No user found with id {id}." });
        }

        return Ok(user);
    }

    // POST: api/users
    [HttpPost]
    public ActionResult<User> CreateUser(User? user)
    {
        if (user is null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (_repository.EmailExists(user.Email))
        {
            _logger.LogWarning("CreateUser: duplicate email {Email}", user.Email);
            return Conflict(new { error = $"A user with email '{user.Email}' already exists." });
        }

        var created = _repository.Add(user);
        _logger.LogInformation("Created user {UserId}", created.Id);
        return CreatedAtAction(nameof(GetUser), new { id = created.Id }, created);
    }

    // PUT: api/users/5
    [HttpPut("{id:int}")]
    public IActionResult UpdateUser(int id, User? user)
    {
        if (id <= 0)
        {
            return BadRequest(new { error = "Id must be a positive integer." });
        }

        if (user is null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (_repository.EmailExists(user.Email, excludeUserId: id))
        {
            _logger.LogWarning("UpdateUser: duplicate email {Email} for id {UserId}", user.Email, id);
            return Conflict(new { error = $"A user with email '{user.Email}' already exists." });
        }

        var updated = _repository.Update(id, user);
        if (!updated)
        {
            _logger.LogWarning("UpdateUser: no user found with id {UserId}", id);
            return NotFound(new { error = $"No user found with id {id}." });
        }

        _logger.LogInformation("Updated user {UserId}", id);
        return NoContent();
    }

    // DELETE: api/users/5
    [HttpDelete("{id:int}")]
    public IActionResult DeleteUser(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new { error = "Id must be a positive integer." });
        }

        var deleted = _repository.Delete(id);
        if (!deleted)
        {
            _logger.LogWarning("DeleteUser: no user found with id {UserId}", id);
            return NotFound(new { error = $"No user found with id {id}." });
        }

        _logger.LogInformation("Deleted user {UserId}", id);
        return NoContent();
    }
}
