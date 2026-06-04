Haul-Matching-System-HMS-
===

## Database - Matching Module

- Migration files live in [HaulMatchingSystem/src/Modules/HMS.Modules.Matching/Migrations](HaulMatchingSystem/src/Modules/HMS.Modules.Matching/Migrations).
- SQL script for PostgreSQL: [Database/02_Matching_Module.sql](Database/02_Matching_Module.sql)

### Apply migration (PostgreSQL)

```powershell
& "C:\Program Files\dotnet\dotnet.exe" ef database update \
	--project "c:\Do_An\Haul-Matching-System-HMS-\HaulMatchingSystem\src\Modules\HMS.Modules.Matching\HMS.Modules.Matching.csproj" \
	--startup-project "c:\Do_An\Haul-Matching-System-HMS-\HaulMatchingSystem\src\HMS.API\HMS.API.csproj" \
	--context MatchingDbContext
```

### Generate migration script

```powershell
& "C:\Program Files\dotnet\dotnet.exe" ef migrations script \
	--project "c:\Do_An\Haul-Matching-System-HMS-\HaulMatchingSystem\src\Modules\HMS.Modules.Matching\HMS.Modules.Matching.csproj" \
	--startup-project "c:\Do_An\Haul-Matching-System-HMS-\HaulMatchingSystem\src\HMS.API\HMS.API.csproj" \
	--context MatchingDbContext \
	--output "c:\Do_An\Haul-Matching-System-HMS-\Database\02_Matching_Module.sql"
```

