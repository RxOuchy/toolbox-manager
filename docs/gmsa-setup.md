# gMSA Setup

This document explains how to provision the Group Managed Service Account that the Polling Service runs under in production. The console apps it spawns inherit this identity — that's how they authenticate to on-prem databases without storing passwords.

> **You only do this once per environment.** Subsequent app deployments don't touch AD.

## Prerequisites

- Active Directory domain with at least one DC running Windows Server 2012+.
- The **KDS Root Key** must exist in the forest. To check:

  ```powershell
  Get-KdsRootKey
  ```

  If the cmdlet returns nothing, an AD admin must create one. In a brand-new lab/test forest, use the immediate-effective variant:

  ```powershell
  # PROD: don't use -EffectiveImmediately; let it propagate naturally
  Add-KdsRootKey -EffectiveImmediately
  ```

- The on-prem Windows host that will run the Polling Service is **already joined to the domain**.

## Step 1 — Create the gMSA

On a DC or a host with the AD PowerShell module:

```powershell
# 1. (Optional) Create a security group of hosts that can use the gMSA — keeps
#    things tidier than scattering ComputerAccount permissions everywhere.
New-ADGroup -Name "Toolbox-Hosts" -GroupScope Global -Path "OU=Groups,DC=corp,DC=example,DC=com"
Add-ADGroupMember -Identity "Toolbox-Hosts" -Members "ToolboxHost01$"

# 2. Create the gMSA itself.
New-ADServiceAccount `
    -Name        "gMSA-Toolbox" `
    -DNSHostName "toolbox.corp.example.com" `
    -PrincipalsAllowedToRetrieveManagedPassword "Toolbox-Hosts"
```

The trailing `$` on the host name (`ToolboxHost01$`) is the computer account in AD — gMSA membership keys off computer accounts, not user accounts.

## Step 2 — Install the gMSA on the polling host

On the polling host, as an admin:

```powershell
# Install the RSAT AD PowerShell feature if missing
Install-WindowsFeature -Name RSAT-AD-PowerShell

# Install the gMSA locally
Install-ADServiceAccount -Identity "gMSA-Toolbox"

# Sanity check — should return $true
Test-ADServiceAccount -Identity "gMSA-Toolbox"
```

If `Test-ADServiceAccount` returns `$false`:

- Re-check the membership: `Get-ADServiceAccount gMSA-Toolbox -Properties PrincipalsAllowedToRetrieveManagedPassword`.
- The host may need to be **rebooted** so the new group membership ticket is in its Kerberos cache.

## Step 3 — Grant database permissions

This is the whole point of using a gMSA. In SQL Server (similar story for Postgres + AD auth):

```sql
USE master;
CREATE LOGIN [CORP\gMSA-Toolbox$] FROM WINDOWS;
USE YourAppDb;
CREATE USER [CORP\gMSA-Toolbox$] FOR LOGIN [CORP\gMSA-Toolbox$];
EXEC sp_addrolemember 'db_datareader', 'CORP\gMSA-Toolbox$';
EXEC sp_addrolemember 'db_datawriter', 'CORP\gMSA-Toolbox$';
```

Mirror the rights granted to whatever account the console apps used to run as before this system existed.

## Step 4 — Install the Polling Service under the gMSA

```powershell
sc.exe create ToolboxManagerPollingService `
    binPath= "`"C:\Program Files\ToolboxManager\PollingService\ToolboxManager.PollingService.exe`"" `
    obj=     "CORP\gMSA-Toolbox$" `
    password= "" `
    start=   auto

# Optional: a recovery profile so it restarts itself on crash
sc.exe failure ToolboxManagerPollingService reset= 60 actions= restart/5000/restart/5000/restart/5000
sc.exe start ToolboxManagerPollingService
```

Note the trailing `$` on the account name and the empty `password=` — both required for gMSA installs. The SCM fetches the current password from AD automatically.

## Step 5 — Verify

```powershell
# Confirm the service is running and what identity it's using
Get-CimInstance Win32_Service -Filter "Name='ToolboxManagerPollingService'" |
    Select-Object Name, State, StartName, ProcessId

# StartName should be CORP\gMSA-Toolbox$
```

Tail the Polling Service logs:

```
C:\Program Files\ToolboxManager\PollingService\logs\toolbox-polling-YYYY-MM-DD.log
```

Then trigger a run from the UI — you should see a child process spawn under the gMSA. Confirm with Process Explorer or:

```powershell
Get-CimInstance Win32_Process -Filter "Name='YourApp.exe'" |
    Select-Object Name, ProcessId, @{n='Owner';e={ $_.GetOwner().User }}
```

## Common pitfalls

| Symptom | Cause | Fix |
|---------|-------|-----|
| `Install-ADServiceAccount` errors with "key does not exist" | KDS root key missing or not yet effective | `Get-KdsRootKey`; if none, an AD admin creates one and waits 10h (prod) or uses `-EffectiveImmediately` (lab) |
| `Test-ADServiceAccount` returns `$false` after install | Computer is not in `PrincipalsAllowedToRetrieveManagedPassword` (or its containing group), or hasn't rebooted to pick up new group membership | Verify membership; reboot host |
| Service fails to start with "logon failure" | `obj=` value missing trailing `$` or wrong domain | `obj= "CORP\gMSA-Toolbox$"` exactly, with the literal `$` |
| Child process runs but DB connection refused | gMSA hasn't been granted DB permissions | Repeat Step 3 against the relevant DB |
| Service starts but won't authenticate to AWS | gMSA has no AWS credentials — that's not what it's for | Use environment variables, IAM role via instance metadata, or AWS SSM for AWS creds; the gMSA is purely for AD-authenticated on-prem resources |

## Why this beats a regular service account

- **No password to rotate** — AD rotates it transparently every 30 days.
- **No password to leak** — it's never anywhere a human can copy it.
- **Scoped** — only the hosts in `PrincipalsAllowedToRetrieveManagedPassword` can use it. Adding/removing hosts is one PowerShell call.
- **Auditable** — login events in Security logs show `CORP\gMSA-Toolbox$` rather than "svc_runner".
