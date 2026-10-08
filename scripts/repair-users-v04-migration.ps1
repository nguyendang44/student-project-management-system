# Repairs the unapplied, empty UserManagementV04Schema migration generated after
# changing ExcludeFromMigrations() on existing mapped entities.
# Does NOT execute "database update" or mutate SQL Server.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$backend = Join-Path $repo 'backend'
$api = Join-Path $backend 'StudentProjects.Api'
$infra = Join-Path $backend 'StudentProjects.Infrastructure'
$migrations = Join-Path $infra 'Persistence\Migrations'
$target = 'UserManagementV04Schema'

if (-not (Test-Path $migrations)) { throw 'No migrations folder found. STOP.' }
if (@(Get-ChildItem $migrations -Filter '*InitialAuth.cs' -File).Count -ne 1) {
    throw 'Cannot identify exactly one InitialAuth migration. STOP.'
}
$files = @(Get-ChildItem $migrations -Filter "*$target.cs" -File)
if ($files.Count -ne 1) { throw 'Expected one UserManagementV04Schema.cs. STOP.' }
$file = $files[0].FullName
$src = Get-Content $file -Raw

# The earlier script stopped before calling database update. This utility must
# only be used with that not-yet-applied migration; do not use on a shared DB.
$upPattern = '(?s)(protected\s+override\s+void\s+Up\(\s*MigrationBuilder\s+migrationBuilder\s*\)\s*\{)\s*(\})'
$downPattern = '(?s)(protected\s+override\s+void\s+Down\(\s*MigrationBuilder\s+migrationBuilder\s*\)\s*\{)\s*(\})'
$up = [regex]::Match($src, $upPattern)
$down = [regex]::Match($src, $downPattern)
if (-not ($up.Success -and $down.Success)) {
    throw 'The migration Up/Down methods are not empty or use an unexpected format. Do not overwrite them. Send this .cs file for review.'
}

$upOps = @'
            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Faculty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "Lecturers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lecturers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lecturers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEntries_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_UserId", table: "Students", column: "UserId", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentCode", table: "Students", column: "StudentCode", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Lecturers_UserId", table: "Lecturers", column: "UserId", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ActorUserId", table: "AuditEntries", column: "ActorUserId");
            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityName_EntityId_CreatedAt", table: "AuditEntries",
                columns: new[] { "EntityName", "EntityId", "CreatedAt" });
'@
$downOps = @'
            migrationBuilder.DropTable(name: "AuditEntries");
            migrationBuilder.DropTable(name: "Lecturers");
            migrationBuilder.DropTable(name: "Students");
'@

# Preserve the generated migration id, designer and model snapshot unchanged.
# Patch ONLY verified empty Up/Down methods. Store a rollback copy.
$backup = "$file.before-v04-repair.bak"
if (-not (Test-Path $backup)) { Copy-Item $file $backup }
function Fill-EmptyMethod([string]$content, [string]$pattern, [string]$code) {
    $m = [regex]::Match($content, $pattern)
    if (-not $m.Success) { throw 'Empty migration method disappeared. STOP.' }
    $replacement = $m.Groups[1].Value + "`r`n" + $code + "`r`n        " + $m.Groups[2].Value
    return $content.Substring(0, $m.Index) + $replacement + $content.Substring($m.Index + $m.Length)
}
$src = Fill-EmptyMethod $src $upPattern $upOps
$src = Fill-EmptyMethod $src $downPattern $downOps
$created = @([regex]::Matches($src, 'migrationBuilder\s*\.\s*CreateTable\(\s*name:\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
if (($created -join ',') -ne 'AuditEntries,Lecturers,Students') {
    throw "Migration verification failed: $($created -join ','). SQL Server unchanged."
}
[System.IO.File]::WriteAllText($file, $src, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'Patched the empty migration Up/Down. SQL Server has NOT been modified.'
Write-Host "Backup: $backup"

Push-Location $repo
try {
    dotnet build '.\backend\StudentProjects.sln'
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. Do not update database.' }
    $sqlPath = Join-Path $repo 'users-v04-schema-preview.sql'
    dotnet ef migrations script InitialAuth $target --project $infra --startup-project $api --context StudentProjectsDbContext --output $sqlPath
    if ($LASTEXITCODE -ne 0) { throw 'Failed to create migration SQL preview. Do not update database.' }
    $sql = Get-Content $sqlPath -Raw
    $tables = @([regex]::Matches($sql, '(?im)^\s*CREATE\s+TABLE\s+(?:\[dbo\]\.)?\[([^\]]+)\]') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
    if (($tables -join ',') -ne 'AuditEntries,Lecturers,Students') {
        throw "SQL creates unexpected tables: $($tables -join ','). Do not update database."
    }
    if ($sql -match '(?im)\b(DROP\s+TABLE|DROP\s+COLUMN|ALTER\s+TABLE\s+\S+\s+DROP)\b') {
        throw 'Unsafe SQL detected. Do not update database.'
    }
    Write-Host 'Build OK. SQL preview verified: only AuditEntries, Lecturers, Students are created.'
    Write-Host "Preview file: $sqlPath"
    Write-Host 'No database changes have been applied by this script.'
    Write-Host 'NEXT, after reviewing SQL preview, run:'
    Write-Host 'dotnet ef database update --project .\backend\StudentProjects.Infrastructure --startup-project .\backend\StudentProjects.Api --context StudentProjectsDbContext'
}
finally { Pop-Location }
