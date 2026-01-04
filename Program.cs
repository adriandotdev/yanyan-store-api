using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        // @TODO: Move to appsettings.json
        ValidIssuer = "sampleissuer",
        ValidAudience = "sampleaudience",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your_super_secret_key_your_super_secret_key_your_super_secret_key"))
    };
});
builder.Services.AddAuthorization();
builder.Services.AddAuthorizationBuilder().AddPolicy("admin_role", policy => policy.RequireRole("admin"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/auth/login", async Task<IResult> () =>
{
    var accessTokenClaims = new[]
    {
      new Claim(JwtRegisteredClaimNames.Sub, "sample"),
      new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
      new Claim(JwtRegisteredClaimNames.Typ, "access_token"),
      new Claim("role", "admin")
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your_super_secret_key_your_super_secret_key_your_super_secret_key"));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var accessToken = new JwtSecurityToken(
        issuer: "sampleissuer",
        audience: "sampleaudience",
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

app.MapPost("/products", async ([FromBody] ProductDTO product, [FromServices] AppDbContext db) =>
{
    await db.Products.AddAsync(new Product
    {
        Name = product.Name,
        Price = product.Price
    });
    await db.SaveChangesAsync();
});

app.MapGet("/products", async ([FromServices] AppDbContext db) =>
{
    return await db.Products.AsNoTracking().ToListAsync();
}).RequireAuthorization("admin_role");

app.MapDelete("/products/{id}", async Task<IResult> (int id, [FromServices] AppDbContext db) =>
{
    var productToBeDeleted = await db.Products.FindAsync(id);

    if (productToBeDeleted is null) return TypedResults.NotFound();

    db.Products.Remove(productToBeDeleted);

    await db.SaveChangesAsync();

    return TypedResults.NoContent();
});

app.MapGet("/products/{id}", async Task<IResult> (int id, [FromServices] AppDbContext db) =>
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

app.MapPut("/products/{id}", async Task<IResult> (int id, [FromBody]UpdateProductDTO product, [FromServices] AppDbContext db) =>
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
