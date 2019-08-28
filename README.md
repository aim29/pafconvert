# PAF Convert

PAF Convert is a tool that converts the [CSV Postcode Address File (PAF)](https://www.poweredbypaf.com/csv-paf/) available from [Royal Mail's Address Management Unit](https://www.poweredbypaf.com/product/paf/) into a fairly flat SQL database.

It creates an addresses table containing the majority of the data from the PAF, with lookups to a localities and thoroughfares table.

A suggested schema for a Microsoft SQL Server database to which this data could be imported is provided, including relevant indexes, views and stored procedures for address and postcode search. A more rudimentary schema is also provided for SQLite, which is the same schema used by the tool itself.

SQLite is used to do the conversion locally, and a GNU-compatible version of sed to do some rudimentary reformatting of the SQL output (removing double quotes from table names to ensure compatibility for import into other RDBMSes like Microsoft SQL Server).

You will want to modify the pafconvert script before running it to suit your purposes, in particular:

* Modifying output table names on lines 128-129, 133-134 and 138-139
* The sed command on line 145 may need modifying/removal

The tool expects to find the `CSV PAF.csv` file in the same folder as it is being run from. It will also create or use a database called `paf.db3` in that same folder, and will write to (or overwrite) an output file called `PAF.sql`.

To run the tool, ensure that you have SQLite and a GNU-compatible sed installed and run:

`sqlite3 paf.db3 ".read pafconvert"`

Note that the SQL database created by the tool will be very large; should you wish to keep it around for later use or for incremental changes I would suggest running a `VACUUM` on it.

The tool is provided with no warranty of fitness for purpose and is released into the public domain.