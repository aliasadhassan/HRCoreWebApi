Solution prr Right Click



Add → New Project

Select: xUnit Test Project

Name:



LearningCoreWebApi.Tests



Target framework:

.NET 8.0



Required NuGet Packages (Test Project)



Test project me ye packages honi chahiye:



xunit

xunit.runner.visualstudio

Moq

Microsoft.AspNetCore.Http





Install-Package Microsoft.EntityFrameworkCore -Version 8.0.0

Install-Package Microsoft.EntityFrameworkCore.SqlServer -Version 8.0.0

Install-Package Microsoft.EntityFrameworkCore.Tools -Version 8.0.0



// install pkg by nuget

Serilog.AspNetCore

Serilog.Settings.Configuration

Serilog.Enrichers.Environment

Serilog.Enrichers.Thread



// remove this portion from appsettings.json file, as we are now handling this logging using serilog props

"Logging": {

&nbsp; "LogLevel": {

&nbsp;   "Default": "Information",

&nbsp;   "Microsoft.AspNetCore": "Warning"

&nbsp; }

},



BCrypt.Net-Next



dotnet add package MailKit

cd LearningCoreWebApi



myaccount.google.com/apppasswords



dotnet clean

dotnet build

Drop-Database

Add-Migration InitialClean

Update-Database
Add-Migration InitialAuthTables
Update-Database

docker build -f LearningCoreWebApi/Dockerfile -t learningcoreapi-image .

docker.io/library/learningcoreapi-image:latest

docker run -d -p 8080:80 --name my-running-api learningcoreapi-image

http://localhost:8080/swagger/index.html

powershell -Command "Test-NetConnection -ComputerName 'localhost' -Port 1433"
sqlservermanager16.msc
docker network ls
netstat -ant

how to add SSO (Single Sign On) on login page
=============================================
User → "Sign in with Microsoft" click →
Microsoft Entra ID login page (redirect) →
User authenticate karta hai Microsoft ke saath →
Entra ID ek Authorization Code Angular app ko wapis bhejta hai →
Angular wo code hr-identity-api ko bhejta hai →
hr-identity-api Microsoft se token verify/exchange karta hai →
hr-identity-api apna khud ka JWT + Refresh Token issue karta hai (jaisa normal login mein karta hai)

Key insight: SSO sirf authentication method badalta hai (password ki jagah Microsoft verify karta hai identity), lekin session management (JWT + refresh token) wahi tumhara existing system rehta hai. Tumhe apna JWT issuance logic dobara likhna nahi parega.

Implementation steps
Backend (hr-identity-api)
NuGet package install karo: Microsoft.Identity.Web (Entra ID ke liye specifically)
Azure Portal mein App Registration banao (Entra ID section mein) — client ID, tenant ID, redirect URI milega
hr-identity-api mein ek naya endpoint banao: /auth/sso/callback — jo Microsoft se aane wale authorization code ko accept kare, verify kare, aur phir apna JWT + refresh token generate kare (same jaisa normal /auth/login karta hai)
Frontend (Angular)
Package: @azure/msal-angular + @azure/msal-browser install karo (Microsoft ka official Angular SSO library)
"Single sign-on (SSO)" button (jo login page mein already hai) pe click handler lagao jo MSAL ke through Microsoft login redirect trigger kare
Redirect wapis aane ke baad, authorization code ko hr-identity-api ke naye endpoint ko bhejo

Sabse behtar: Microsoft Entra ID (Azure AD)

Wajah:

Tumhara stack already Azure pe hai — VM, hosting, sab Azure ecosystem mein hai. Entra ID integrate karna sabse natural fit hai, alag se koi naya cloud account/setup nahi chahiye
HR SaaS ka target market — jo bhi companies is product ko use karengi, wo aksar already Microsoft 365 / Office 365 use kar rahi hongi apne staff ke liye. Unke employees pehle se hi Microsoft account rakhte hain — SSO setup unke IT admin ke liye chand click ka kaam hoga (Entra ID mein app registration approve karna)
Enterprise-ready feel — jab koi company HR software select karti hai, "Sign in with Microsoft" dekh kar unhe bharosa aata hai ke ye enterprise-grade security follow karta hai — ye ek sales/credibility factor bhi hai
Best Angular support — Microsoft ka apna official library @azure/msal-angular hai, well-maintained, achi documentation, Angular ke saath first-class integration

install this nuget Package in hr-identity-api
Microsoft.Identity.Web

main benefit of SSO
Employees ko baar-baar alag-alag systems mein password na dalne pare — ek hi (Microsoft) login se sab jagah access mil jata hai, aur security bhi centralize ho jati hai.

Step-by-step shuru karne ka tareeqa
Step 1: App Registration banao (Azure Portal mein)
portal.azure.com → search karo "Microsoft Entra ID"
Left menu mein "App registrations" → "+ New registration"
Fill karo:
Name: HR Cloud (ya jo bhi app ka naam)
Supported account types: "Accounts in this organizational directory only" (agar sirf single company ke liye hai) ya "Accounts in any organizational directory" (agar multi-tenant/multiple companies ke liye SaaS banana hai — ye wala select karo, kyunke tumhara HR SaaS multiple client companies ko serve karega)
Redirect URI: Type = "Single-page application (SPA)", URL = http://localhost:4200 (development ke liye; baad mein https://hr-cloud.online bhi add karna hoga)
Register pe click karo
Step 2: Client ID aur Tenant ID note karo

Registration ke baad Overview page pe ye dikhega:

Application (client) ID
Directory (tenant) ID

Ye dono values Angular app ke config mein use hongi.

Step 3: API permissions set karo
Left menu mein "API permissions" → confirm karo User.Read permission already hai (default hota hai)

94e07c71-1694-417b-bedb-59a982205dc8
2e27f79b-a47d-49f5-aa41-9c5fd4a1cc60

Angular side:
Step 1: Package install karo
npm install @azure/msal-angular @azure/msal-browser

Step 2: MSAL config file banao
src/app/auth/msal-config.ts naam se naya file banao:

import { LogLevel, PublicClientApplication, IPublicClientApplication } from '@azure/msal-browser';

export function MSALInstanceFactory(): IPublicClientApplication {
  return new PublicClientApplication({
    auth: {
      clientId: 'YOUR_CLIENT_ID_HERE',        // Overview page se copy karo
      authority: 'https://login.microsoftonline.com/common', // multi-tenant ke liye 'common'
      redirectUri: 'http://localhost:4200',    // dev ke liye; prod mein hr-cloud.online
      postLogoutRedirectUri: 'http://localhost:4200/login',
    },
    cache: {
      cacheLocation: 'localStorage',           // taake refresh pe session na ude
      storeAuthStateInCookie: false,
    },
    system: {
      loggerOptions: {
        loggerCallback: () => {},
        logLevel: LogLevel.Warning,
      },
    },
  });
}
Step 4: login.ts mein SSO button ko wire karo

import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { MsalService } from '@azure/msal-angular';
// ... baaki existing imports (AuthService, Router, waghera)

export class LoginComponent {
  today = new Date();
  hidePassword = true;

  constructor(
    // ... existing injections
    private msalService: MsalService
  ) {}

  signInWithSSO(): void {
    this.msalService.loginPopup({
      scopes: ['user.read']
    }).subscribe({
      next: (result) => {
        console.log('Microsoft login successful:', result);
        // Yahan se access token backend (hr-identity-api) ko bhejenge
        this.sendTokenToBackend(result.accessToken);
      },
      error: (error) => {
        console.error('SSO login failed:', error);
      }
    });
  }

  private sendTokenToBackend(microsoftToken: string): void {
    // TODO: apna AuthService method call karo jo ye token 
    // hr-identity-api ke naye /auth/sso/callback endpoint ko bhejay
    // aur wahan se apna JWT + refresh token wapis le
  }
}

Step 5: login.html mein button pe click event lagao

Jo already tha:
<button type="button" class="sso-btn">Single sign-on (SSO)</button>

Isay update karo:
<button type="button" class="sso-btn" (click)="signInWithSSO()">Single sign-on (SSO)</button>

Zaroori: YOUR_CLIENT_ID_HERE ki jagah wo Application (client) ID paste karo jo Overview page se mili thi.

Perfect, ab backend side dekhte hain. Approach simple hai: Angular se jo Microsoft token aayega, backend usay Microsoft Graph API se verify karega (real Microsoft user hai ya nahi), phir apna existing JWT + refresh token generate karega — bilkul normal login jaisa flow, bas password check ki jagah Microsoft verification hai.

Step 1: DTO banao

hr-identity-api mein Models/SsoLoginRequest.cs:

namespace HR.Identity.API.Models
{
    public class SsoLoginRequest
    {
        public string AccessToken { get; set; } = string.Empty;
    }
}
Step 2: Microsoft Graph se user verify karne wali service

Services/MicrosoftGraphService.cs:

using System.Net.Http.Headers;
using System.Text.Json;

namespace HR.Identity.API.Services
{
    public class MicrosoftGraphUser
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class MicrosoftGraphService
    {
        private readonly HttpClient _httpClient;

        public MicrosoftGraphService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<MicrosoftGraphUser?> GetUserFromTokenAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return null; // Invalid ya expired token
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new MicrosoftGraphUser
            {
                Id = root.GetProperty("id").GetString() ?? string.Empty,
                DisplayName = root.GetProperty("displayName").GetString() ?? string.Empty,
                Email = root.TryGetProperty("mail", out var mail) && mail.ValueKind != JsonValueKind.Null
                    ? mail.GetString() ?? string.Empty
                    : root.GetProperty("userPrincipalName").GetString() ?? string.Empty
            };
        }
    }
}
(Note: kabhi kabhi mail field null hoti hai — is liye fallback userPrincipalName rakha, jo aksar email jaisa hi hota hai)

Step 3: Program.cs mein service register karo
builder.Services.AddHttpClient<MicrosoftGraphService>();

Step 4: Controller endpoint banao
Controllers/AuthController.cs mein (jahan tumhara existing /login endpoint hai, wahin add karo):
[HttpPost("sso/callback")]
public async Task<IActionResult> SsoCallback([FromBody] SsoLoginRequest request)
{
    // Step 1: Microsoft se user verify karo
    var msUser = await _graphService.GetUserFromTokenAsync(request.AccessToken);

    if (msUser == null)
    {
        return Unauthorized(new { message = "Invalid Microsoft token" });
    }

    // Step 2: Check karo ye user tumhare system mein already exist karta hai ya nahi
    var existingUser = await _userManager.FindByEmailAsync(msUser.Email);

    if (existingUser == null)
    {
        // Option A: Naya user auto-create kar do
        existingUser = new ApplicationUser
        {
            UserName = msUser.Email,
            Email = msUser.Email,
            EmailConfirmed = true // Microsoft ne already verify kiya hai
        };

        var createResult = await _userManager.CreateAsync(existingUser);
        if (!createResult.Succeeded)
        {
            return BadRequest(new { message = "Failed to create user", errors = createResult.Errors });
        }

        // Option B (agar chaho): sirf pre-registered users allow karo
        // return Unauthorized(new { message = "No account found for this email. Contact your admin." });
    }

    // Step 3: Apna normal JWT + refresh token generate karo (jaisa /login endpoint karta hai)
    var jwtToken = _tokenService.GenerateJwtToken(existingUser);
    var refreshToken = _tokenService.GenerateRefreshToken();

    // Refresh token ko DB mein save karo (jaisa normal login mein karte ho)
    existingUser.RefreshToken = refreshToken;
    existingUser.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
    await _userManager.UpdateAsync(existingUser);

    return Ok(new
    {
        token = jwtToken,
        refreshToken = refreshToken,
        email = existingUser.Email
    });
}
(_graphService, _userManager, _tokenService — ye teeno constructor mein inject hone chahiye, jaisa tumhare existing controller mein already _tokenService/_userManager istemal ho rahe honge normal login ke liye)

Step 5: Angular ka sendTokenToBackend complete karo
private sendTokenToBackend(microsoftToken: string): void {
  this.authService.ssoLogin(microsoftToken).subscribe({
    next: (response) => {
      // Jaisa normal login response handle karte ho waisa hi
      localStorage.setItem('token', response.token);
      localStorage.setItem('refreshToken', response.refreshToken);
      this.router.navigate(['/dashboard']);
    },
    error: (error) => {
      console.error('SSO backend exchange failed:', error);
    }
  });
}

auth.ts (AuthService) mein naya method add karo:
ssoLogin(accessToken: string): Observable<any> {
  return this.http.post(`${this.apiUrl}/auth/sso/callback`, { accessToken });
}

Password wale normal login mein: user email/password type karta hai, backend seedha DB se check karta hai
SSO mein: password ki jagah Microsoft verify karta hai ke user asli hai, phir backend usi Microsoft-verified identity ke liye apna khud ka JWT bana deta hai — is liye session management (JWT + refresh token) tumhare purane system jaisa hi rehta hai, sirf "kaise pata chala ke ye sahi user hai" wala hissa badalta hai

Ek zaroori security cheez jo diagram mein implicit hai: Step 5 (Graph API verify) is liye zaroori hai kyunke Angular se aane wala token trust nahi kiya ja sakta bina check kiye — koi bhi fake token bhej sakta hai. Microsoft Graph se confirm karwana hi guarantee deta hai ke ye genuine Microsoft-authenticated user hai.

Tum SSO button dabao ge, Microsoft se aliasadhassan@outlook.com login karoge
Backend Microsoft Graph se verify karega ke ye genuine user hai
Backend database mein check karega — chunki ye email pehli baar aa rahi hai, user == null hoga
Backend automatically ek naya row Users table mein create kar dega (with DisplayName se username, aur ek random unusable password hash — kyunke ye user kabhi password se login nahi karega)
Fir usi naye user ke liye JWT + refresh token issue ho jayega, aur tum seedha dashboard tak pahunch jaoge

first time to login ho jaega sso sy ..lekin second time jab me login kruga to password konsa duga?

SSO login ke baad password authentication ke constraints samjhaye

Achha sawal — aur ye important cheez hai samajhna.

Second/agli baar bhi tumhe SSO se hi login karna hoga, normal email/password se nahi. Wajah:

Jab backend aliasadhassan@outlook.com ke liye naya user auto-create karta hai, hum ye line likhte hain:

csharp
PasswordHash = PasswordHelper.Hash(Guid.NewGuid().ToString())

Ye ek random, unknown password ka hash hai — koi bhi human isay nahi jaanta, na tum, na koi aur. Isay is liye rakha gaya tha kyunke PasswordHash field database mein required hai (khali nahi ho sakti), lekin humara irada ye tha ke ye user kabhi normal password se login na kare — sirf SSO se.

Matlab:

✅ SSO button se — hamesha kaam karega (Microsoft verify karta hai identity, password ki zarurat nahi)
❌ Normal login form se (email + password) — kaam nahi karega, kyunke password koi jaanta hi nahi

Ye actually sahi/expected behavior hai real-world SSO systems mein — jo user Microsoft se sign in karta hai, wo hamesha Microsoft se hi sign in karega, alag se password set nahi karta (jab tak explicitly "set a password" wala feature na ho).

Agar tum chahte ho ke ye user password se bhi login kar sake (dono tareeqe available hon), to ek extra feature banana parega: "Set password" option jab user pehli baar SSO se login kare, jahan wo apna khud ka password choose kar sake — us case mein backend mein ek chota sa update endpoint banana hoga jo authenticated user ke PasswordHash ko replace kare.