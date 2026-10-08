# YantraSearch .NET API

UI-free ASP.NET Web API (.NET Framework 4.8) for the React app. It uses the same MySQL database as `yantrasearch-backend-dotnet` (`YantraNew_DB`). Razor pages, Tabler desks, and `Content/` are not in this project.

Routes match the Java API the React app already calls: `/api/v1/auth/*`, `/api/v1/equipment`, `/api/v1/contractors`, `/api/v1/job-seekers`, `/api/v1/supplier-offerings`, `/api/v1/requirements`, `/api/v1/payments/upi/*`, `/api/v1/admin/*`, `/api/enquiry`.

Passwords stay ASP.NET Identity hashes. Login returns a Bearer JWT whose claims (`sub`, `role`, `tier`, `paymentEnabled`) are what the React login page reads. Roles are mapped as Vendor → SUPPLIER and Employee → JOB_SEEKER.

## Run locally

1. On the MySQL database, run `Migrations/Scripts/AddJavaParityTables_MySQL.sql` once if those tables are not there yet. It only adds tables and a nullable phone column.
2. Open `YantraSearch.Api.csproj` in Visual Studio on Windows. IIS Express is set to `http://localhost:8090/`.
3. Copy `connectionStrings.config.example`, `mailSettings.config.example`, and `secrets.config.example` to `connectionStrings.config`, `mailSettings.config`, and `secrets.config` in this same folder. Fill in the database password, Gmail app password, and JWT secret. Those three files are gitignored. On the server they sit next to `Web.config`.
4. In the React app, set `VITE_API_PROXY_TARGET=http://localhost:8090` (already the default in `vite.config.ts`) and run `npm run dev`. Leave `apiBaseUrl` blank in `config.local.json` so the browser calls `/api` on the Vite server and Vite forwards it here.

## Grabweb

Publish this project (not the MVC Dashboards site) to its own IIS application. Point the React build’s `config.in.json` `apiBaseUrl` at that site if React and the API are on different hosts. If they share one site, leave `apiBaseUrl` empty. Copy `Uploads/` onto that site, or set `PublicFileBase` to `https://www.yantrasearch.com` so listing images still load from the live site.
