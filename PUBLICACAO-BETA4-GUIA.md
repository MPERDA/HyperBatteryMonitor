# Hyper Battery Monitor — Publicação v2.4.0-beta.4

**Estado:** pacote de publicação preparado. Nem build Windows nem publicação GitHub foram executados neste ambiente.

**Base obrigatória:** código da Beta 3 publicada + patch **Beta 4 REV2** já aplicado. `HyperXBatteryMonitor.csproj` deve declarar `Version=2.4.0`, `ReleaseChannel=Beta`, `BetaNumber=4`.

Copie os arquivos da pasta `Release/` deste pacote para `C:\Projetos\HyperX Battery Monitor\Source Code\Release\` (substitua as Release Notes da Beta 4, não modifique o script Beta 3). Na raiz do repositório:

```powershell
cd 'C:\Projetos\HyperX Battery Monitor\Source Code'

# Inspecione as alterações, confira que a Beta 4 REV2 está aplicada e teste a aplicação.
git status -sb
git diff --check
dotnet build -c Debug
if ($LASTEXITCODE -ne 0) { throw 'Build Debug falhou' }

# Após smoke test local do seu Cloud III e do Auto Detect, registre as mudanças.
git add -A
if ($LASTEXITCODE -ne 0) { throw 'git add falhou' }
git diff --cached --check
if ($LASTEXITCODE -ne 0) { throw 'git diff --cached --check falhou' }
git diff --cached --stat
```

Revise os arquivos staged antes do commit. Não inclua executáveis, pastas `bin/obj`, dados privados ou arquivos temporários.

```powershell
git commit -m 'release: prepare v2.4.0-beta.4'
if ($LASTEXITCODE -ne 0) { throw 'Commit falhou' }
git push origin develop/v2.4.0
if ($LASTEXITCODE -ne 0) { throw 'Push falhou' }

# Primeira execução: apenas build Release, installer e validações.
.\Release\Publish-Beta4.ps1
```

Confira `PREPARATION COMPLETE - NOTHING PUBLISHED.`, o caminho/nome do instalador, o tamanho, o SHA-256 e as Release Notes. Se houver erro, **não** execute `-Publish`.

```powershell
# Somente após a preparação ser concluída e o instalador ser aprovado:
.\Release\Publish-Beta4.ps1 -Publish
```

Confirme com `PUBLISH` apenas após verificar a saída. O script recompila, cria uma GitHub Pre-release (`--latest=false`), verifica flags, nome, tamanho, hash e commit da tag. Resultado esperado: `BETA 4 PUBLISHED AND VERIFIED.`

**Não altere nem reutilize tags `.beta.1`, `.beta.2`, `.beta.3`.** O script aborta caso a tag `.beta.4` já exista.

**Teste de aceitação:** executar o HBM localmente com seu Cloud III (bateria, reconexão, Auto Detect); depois da publicação, pedir ao tester Cloud III S `03F0:02CC` para confirmar bateria, detecção manual e automática, e reconexão.
