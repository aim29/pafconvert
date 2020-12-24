# PAFConvert

PAFConvert is a tool that converts the [CSV Postcode Address File (PAF)](https://www.poweredbypaf.com/csv-paf/) available from [Royal Mail's Address Management Unit](https://www.poweredbypaf.com/product/paf/) into a fairly flat Microsoft SQL Server database.

It creates an addresses table containing the majority of the data from the PAF, with lookups to a localities and thoroughfares table.

The expected schema for a Microsoft SQL Server database to which this data could be imported is provided, including relevant indexes, views and stored procedures for address and postcode search.

The tool is written in .NET 5 and is therefore cross-platform. [CSVHelper](https://joshclose.github.io/CsvHelper/) is used to import data quickly from CSV. [SQLite](https://sqlite.org) is used to do the conversion internally.

---

## Using the tool

You may need to compile the application.

1. Ensure you have the .NET 5.0 console runtime installed.
2. Configure the database connection strings in appsettings.json.   
   Note that the default `StagingDB` value of `Data Source=:memory:` creates an ephemeral in-memory database for the conversion. This takes about 3GB of RAM but is quickest.
3. Place the `CSV PAF.csv` file in the application directory.
3. Run the tool: `dotnet PAFConvert.dll`.

---

## Licensing

The tool is provided with no warranty of fitness for purpose and is released under the MIT Licence.
