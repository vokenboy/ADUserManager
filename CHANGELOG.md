# Changelog

All notable changes to this project will be documented in this file.

## [0.0.4] - 2026-02-09

### Added
- Initial release of ActiveManager
- Active Directory user management functionality
- User search and filtering capabilities
- Group management features
- Fire user functionality with multiple options:
  - Disable user account
  - Move to disabled OU
  - Change password
  - Remove from groups
  - Export user data
- Single-file executable for easy distribution
- PowerShell install script for automated deployment
- Windows Forms-based user interface

### Technical Details
- Built with .NET 10
- Self-contained single-file executable
- Supports Windows 10/11
- Uses System.DirectoryServices for AD integration
