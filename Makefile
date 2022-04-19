compile:
	@dotnet build
	@mkdir -p build/module
	@cp -rf bin/Debug/net6.0/. build/module
package:
	@./buildpackage.sh
