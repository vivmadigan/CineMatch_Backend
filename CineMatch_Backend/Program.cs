using Infrastructure.Data.Context;
using Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
  .AddIdentityCore<UserEntity>(o =>
  {
      o.User.RequireUniqueEmail = true;
  })
  .AddRoles<IdentityRole>()
  .AddEntityFrameworkStores<ApplicationDbContext>();
          



builder.Services.AddControllers();

builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(x => x.SwaggerEndpoint("/swagger/v1/swagger.json", "CineMatch v1"));

// Redirect root URL "/" to "/swagger"
// "^$" is a regular expression that matches an empty path (i.e., exactly the site root).
app.UseRewriter(new RewriteOptions().AddRedirect("^$", "swagger"));
app.UseHttpsRedirection();

app.UseAuthorization();

// Enable CORS for all origins, methods, and headers
// Can be customized as needed for security
app.UseCors(policy =>
{
    policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader();
});

app.MapControllers();

app.Run();
