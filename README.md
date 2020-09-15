# dfc-providerportal-tribalexporter

[![Build status](https://sfa-gov-uk.visualstudio.com/Digital%20First%20Careers/_apis/build/status/Find%20an%20Opportunity/dfc-providerportal-tribalexporter?branchName=main)](https://sfa-gov-uk.visualstudio.com/Digital%20First%20Careers/_build/results?buildId=245254&view=results)

A set of Azure Function Apps that operate on the NCS Course Directory data.

## Running the app host locally

Ask the team for a copy of `local.settings.json` and place it in `src/Dfc.ProviderPortal.TribalExporter/`.

### Run the host

```
cd src/Dfc.ProviderPortal.TribalExporter/
ASPNETCORE_ENVIRONMENT=Development AzureFunctionsJobHost__logging__logLevel__default=Debug ENVIRONMENT=development func host start --csharp
```

### Trigger the functions

Vist the function listing at http://localhost:7071/admin/functions - you should see all the functions listed.

Trigger a function with:

```
curl -v http://localhost:7071/admin/functions/FunctionNameHere -d "{}" -H "Content-Type:application/json"
```
