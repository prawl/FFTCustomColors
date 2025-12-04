# Changelog

All notable changes to FFT Color Mod will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Function hooking strategy based on FFTGenericJobs analysis
- Build automation with PowerShell scripts (BuildLinked.ps1, Publish.ps1)
- GitHub Actions CI/CD workflow
- Signature scanning implementation plan
- Comprehensive documentation updates

### Changed
- Switched from file interception to function hooking approach
- Updated project to target .NET 8.0 Windows
- Enhanced build configuration for IL trimming support

## [0.3.0] - 2025-12-03

### Added
- Analysis of FFTGenericJobs mod revealing working memory manipulation approach
- PowerShell build scripts for development and release
- GitHub Actions workflow for automated builds
- Function hooking strategy documentation

### Changed
- Primary approach from file interception to function hooking
- Build process to support Reloaded-II packaging standards

## [0.2.0] - 2025-12-02

### Added
- 27 passing unit tests with TDD framework
- Complete Chapter 1-4 support for all Ramza outfit variations
- Memory manipulation research documentation
- File interception approach identification

### Changed
- Identified FFT's dynamic palette reloading behavior
- Determined file interception as initial solution

## [0.1.0] - 2025-12-01

### Added
- Initial project setup with Reloaded-II framework
- TDD implementation with xUnit and FluentAssertions
- Color detection and replacement logic
- Hotkey system (F1: Original, F2: Red)
- Basic mod structure and configuration