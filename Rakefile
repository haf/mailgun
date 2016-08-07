require 'bundler/setup'

require 'albacore'
require 'albacore/tasks/release'
require 'albacore/tasks/versionizer'
require 'albacore/ext/teamcity'

Configuration = ENV['CONFIGURATION'] || 'Release'

Albacore::Tasks::Versionizer.new :versioning

task :paket_replace do
  sh %{ruby -pi.bak -e "gsub(/namespace Logary.Facade/, 'namespace Mailgun.Logging')" paket-files/logary/logary/src/Logary.Facade/Facade.fs}
end

desc 'create assembly infos'
asmver_files :assembly_info do |a|
  a.files = FileList['**/*proj'] # optional, will find all projects recursively by default

  a.attributes assembly_description: 'A Mailgun F# HTTPS API wrapper',
               assembly_configuration: Configuration,
               assembly_company: 'Logibit AB',
               assembly_copyright: '(c) 2015 by Henrik Feldt',
               assembly_version: ENV['LONG_VERSION'],
               assembly_file_version: ENV['LONG_VERSION'],
               assembly_informational_version: ENV['BUILD_VERSION']
  a.handle_config do |proj, conf|
    conf.namespace = "Mailgun"
    conf
  end
end

desc 'Perform fast build (warn: doesn\'t d/l deps)'
build :quick_compile do |b|
  b.prop 'Configuration', Configuration
  b.logging = 'detailed'
  b.sln     = 'src/Mailgun.Api.sln'
end

task :paket_bootstrap do
system 'tools/paket.bootstrapper.exe', clr_command: true unless File.exists? 'tools/paket.exe'
end

task :paket_restore do
  system 'tools/paket.exe', 'restore', clr_command: true
end

desc 'restore all nugets as per the packages.config files'
task :restore => [:paket_bootstrap, :paket_restore, :paket_replace]

desc 'Perform full build'
build :compile => [:versioning, :restore, :assembly_info] do |b|
  b.prop 'Configuration', Configuration
  b.sln = 'src/Mailgun.Api.sln'
end

directory 'build/pkg'

desc 'package nugets - finds all projects and package them'
nugets_pack :create_nugets => ['build/pkg', :versioning, :compile] do |p|
  p.configuration = Configuration
  p.files   = FileList['src/**/*.{csproj,fsproj,nuspec}'].
    exclude(/Tests/)
  p.out     = 'build/pkg'
  p.exe     = 'packages/NuGet.CommandLine/tools/NuGet.exe'
  p.with_metadata do |m|
    # m.id          = 'MyProj'
    m.title       = 'Mailgun.Api'
    m.description = 'A Mailgun F# HTTPS API wrapper'
    m.authors     = 'Henrik Feldt, Logibit AB'
    m.project_url = 'https://github.com/haf/mailgun'
    m.tags        = 'email mail e-mail mailgun api'
    m.version     = ENV['NUGET_VERSION']
  end
end

namespace :tests do
  task :unit do
    system "src/Mailgun.Api.Tests/bin/#{Configuration}/Mailgun.Api.Tests.exe", clr_command: true
  end
end

task :tests => :'tests:unit'

task :default => [:create_nugets, :tests]

task :ensure_nuget_key do
  raise 'missing env NUGET_KEY value' unless ENV['NUGET_KEY']
end

Albacore::Tasks::Release.new :release,
                             pkg_dir: 'build/pkg',
                             depend_on: [:create_nugets, :tests, :ensure_nuget_key],
                             nuget_exe: 'packages/NuGet.CommandLine/tools/NuGet.exe',
                             api_key: ENV['NUGET_KEY']
