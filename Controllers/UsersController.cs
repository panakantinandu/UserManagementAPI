using Microsoft.AspNetCore.Mvc;
using UserManagementAPI.Models;
using UserManagementAPI.Services;

namespace UserManagementAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _repository;

    public UsersController(IUserRepository repository)
    {
        _repository = repository;
    }

    // GET: api/users
    [HttpGet]
    public ActionResult<IEnumerable<User>> GetUsers()
    {
        return Ok(_repository.GetAll());
    }

    // GET: api/users/5
    [HttpGet("{id:int}")]
    public ActionResult<User> GetUser(int id)
    {
        var user = _repository.GetById(id);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    // POST: api/users
    [HttpPost]
    public ActionResult<User> CreateUser(User user)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var created = _repository.Add(user);
        return CreatedAtAction(nameof(GetUser), new { id = created.Id }, created);
    }

    // PUT: api/users/5
    [HttpPut("{id:int}")]
    public IActionResult UpdateUser(int id, User user)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var updated = _repository.Update(id, user);
        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }

    // DELETE: api/users/5
    [HttpDelete("{id:int}")]
    public IActionResult DeleteUser(int id)
    {
        var deleted = _repository.Delete(id);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
