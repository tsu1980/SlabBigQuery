# v0.1.3
* Fix Mamemaki.Slab.BigQuery.nuspec.template.xml deploy as content
* Rename onCompletedTimeout to bufferingFlushAllTimeout
* Set X509KeyStorageFlags.MachineKeySet to import private key file for working in Azure webapps

# v0.1.2
* Added Mamemaki.Slab.BigQuery.Service(Out-of-process Windows Service for SLAB, with Mamemaki.Slab.BigQuery)
* Upload Out-Of-Process packages to GitHub release page
* Fix BigQueryInsertFault message doesn't output problem

# v0.1.1
* Change default values of bufferingInterval, bufferingCount, maxBufferSize.
* Rename options name to privateKeyFile, privateKeyPassphrase.
* In CreateRowData, throw NoValueException if field mode is "REQUIRED".
* Add README.md

# v0.1
Initial release
