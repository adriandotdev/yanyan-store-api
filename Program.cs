using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var JwtConfig = builder.Configuration.GetSection("Jwt");
var ISSUER = JwtConfig["Issuer"];
var AUDIENCE = JwtConfig["Audience"];
var SECRET_KEY = JwtConfig["SecretKey"];

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = ISSUER,
        ValidAudience = AUDIENCE,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SECRET_KEY!))
    };
});
builder.Services.AddAuthorization();
builder.Services.AddAuthorizationBuilder().AddPolicy("admin_role", policy => policy.RequireRole("Admin"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Auth APIs
app.MapPost("/api/v1/auth/login", async Task<IResult> ([FromBody] LoginDTO payload, [FromServices] AppDbContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(user => user.Username.Equals(payload.Username));

    if (user is null) return TypedResults.NotFound();

    // @TODO must be in bcrypt
    if (user.Password != payload.Password) return TypedResults.Unauthorized();

    var accessTokenClaims = new[]
    {
      new Claim(JwtRegisteredClaimNames.Sub, user.Username), 
      new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
      new Claim(JwtRegisteredClaimNames.Typ, "access_token"),
      new Claim("role", user.Role.ToString())
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SECRET_KEY!));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var accessToken = new JwtSecurityToken(
        issuer: ISSUER,
        audience: AUDIENCE,
        claims: accessTokenClaims,
        expires: DateTime.Now.AddMinutes(15),
        signingCredentials: creds
    );

    var returnAccessToken = new JwtSecurityTokenHandler().WriteToken(accessToken);

    return TypedResults.Ok(new
    {
        data = new
        {
            AccessToken = returnAccessToken
        }
    });
});

// Product APIs
app.MapPost("/api/v1/products", async ([FromBody] ProductDTO product, [FromServices] AppDbContext db) =>
{
    await db.Products.AddAsync(new Product
    {
        Name = product.Name,
        Price = product.Price
    });
    await db.SaveChangesAsync();
});

app.MapGet("/api/v1/products", async ([FromServices] AppDbContext db) =>
{
    return await db.Products.AsNoTracking().ToListAsync();
}).RequireAuthorization("admin_role");

app.MapDelete("/api/v1/products/{id}", async Task<IResult> (int id, [FromServices] AppDbContext db) =>
{
    var productToBeDeleted = await db.Products.FindAsync(id);

    if (productToBeDeleted is null) return TypedResults.NotFound();

    db.Products.Remove(productToBeDeleted);

    await db.SaveChangesAsync();

    return TypedResults.NoContent();
});

app.MapGet("/api/v1/products/{id}", async Task<IResult> (int id, [FromServices] AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);

    if (product is null) return TypedResults.NotFound();

    return TypedResults.Ok(new
    {
        Data = product,
        Message = $"Successfully retrieved the product with ID of {id}",
        Success = true
    });
});

app.MapPut("/api/v1/products/{id}", async Task<IResult> (int id, [FromBody]UpdateProductDTO product, [FromServices] AppDbContext db) =>
{
    var productToBeUpdated = await db.Products.FindAsync(id);

    if (productToBeUpdated is null) return TypedResults.NotFound(new
    {
        Message = $"Product with ID of {id} is not found"
    });

    productToBeUpdated.Name = product.Name ?? productToBeUpdated.Name;
    productToBeUpdated.Price = product.Price ?? productToBeUpdated.Price;

    await db.SaveChangesAsync();

    return TypedResults.Ok();
});

app.Run();
