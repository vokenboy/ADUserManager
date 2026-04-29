Add-Type -AssemblyName System.Windows.Forms

Add-Type -AssemblyName System.Drawing

Import-Module ActiveDirectory

Import-Module ExchangeOnlineManagement

Import-Module Microsoft.Graph.Users.Actions
 
$logFile = "offboarding_errors.log"
 
function Log-Error {

    param($step, $error)

    $shortMessage = "$step - error encountered. Check log file for details.`r`n"

    $detailedMessage = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') [$step] $error`r`n"

    $logBox.AppendText($shortMessage)

    Add-Content -Path $logFile -Value $detailedMessage

}
 
function Generate-RandomPassword {

    Add-Type -AssemblyName System.Web

    return [System.Web.Security.Membership]::GeneratePassword(12, 4)

}
 
function Get-UserGroups {

    param($dn)

    try {

        $user = Get-ADUser -Identity $dn -Properties MemberOf

        return $user.MemberOf

    } catch {

        Log-Error "Get-UserGroups" $_

        return @()

    }

}
 
function Remove-UserFromGroups {

    param($dn, $groups)

    foreach ($groupDN in $groups) {

        try {

            Remove-ADGroupMember -Identity $groupDN -Members $dn -Confirm:$false

        } catch {

            Log-Error "Remove-UserFromGroups" $_

        }

    }

}
 
function Rename-User {

    param($dn, $date)

    try {

        $user = Get-ADUser -Identity $dn -Properties GivenName, Surname

        $newName = "$($user.GivenName) $($user.Surname) LEFT $date"

        Set-ADUser -Identity $dn -DisplayName $newName

        Rename-ADObject -Identity $dn -NewName $newName

    } catch {

        Log-Error "Rename-User" $_

    }

}
 
function Set-Autoreply {

    param($upn, $date, $firstName, $lastName)

    try {

        $messageLT = "Dėkojame Jums už laišką. Nuo $date $firstName $lastName nebedirba DOJUS įmonių grupėje. Maloniai prašome kreiptis: info@dojusagro.lt, Mob. tel. +370 68250054."

        $messageEN = "Thank you for your letter. From $date $firstName $lastName no longer works in the DOJUS group, so kindly please go in contact with : info@dojusagro.lt, Mob. tel. +370 68250054."

        $fullMessage = "$messageLT`n`n$messageEN"

        Set-MailboxAutoReplyConfiguration -Identity $upn -AutoReplyState Enabled -InternalMessage $fullMessage -ExternalMessage $fullMessage

    } catch {

        Log-Error "Set-Autoreply" $_

    }

}
 
function Remove-CloudGroups {

    param($upn)

    try {

        $groups = Get-MgUserMemberOf -UserId $upn

        $logBox.AppendText("Cloud groups:`r`n")

        foreach ($group in $groups) {

            $groupId = $group.Id

            $groupName = $group.AdditionalProperties.displayName

            $logBox.AppendText("$groupName`r`n")

            try {

                Remove-MgGroupMemberByRef -GroupId $groupId -DirectoryObjectId $upn

            } catch {

                Log-Error "Remove-CloudGroup $groupName" $_

            }

        }

    } catch {

        Log-Error "List-CloudGroups" $_

    }

}
 
# GUI setup

$form = New-Object System.Windows.Forms.Form

$form.Text = "Išdarbinimo įrankis"

$form.Size = New-Object System.Drawing.Size(600, 700)

$form.StartPosition = "CenterScreen"
 
$searchLabel = New-Object System.Windows.Forms.Label

$searchLabel.Text = "Ieškoti AD vartotojo:"

$searchLabel.Location = New-Object System.Drawing.Point(20, 20)

$searchLabel.Size = New-Object System.Drawing.Size(200, 20)

$form.Controls.Add($searchLabel)
 
$searchBox = New-Object System.Windows.Forms.TextBox

$searchBox.Location = New-Object System.Drawing.Point(20, 45)

$searchBox.Size = New-Object System.Drawing.Size(400, 20)

$form.Controls.Add($searchBox)
 
$searchButton = New-Object System.Windows.Forms.Button

$searchButton.Text = "Ieškoti"

$searchButton.Location = New-Object System.Drawing.Point(430, 42)

$searchButton.Size = New-Object System.Drawing.Size(80, 25)

$form.Controls.Add($searchButton)
 
$userList = New-Object System.Windows.Forms.ListBox

$userList.Location = New-Object System.Drawing.Point(20, 75)

$userList.Size = New-Object System.Drawing.Size(540, 100)

$form.Controls.Add($userList)
 
$selectedLabel = New-Object System.Windows.Forms.Label

$selectedLabel.Text = "Pasirinktas vartotojas:"

$selectedLabel.Location = New-Object System.Drawing.Point(20, 185)

$selectedLabel.Size = New-Object System.Drawing.Size(200, 20)

$form.Controls.Add($selectedLabel)
 
$selectedBox = New-Object System.Windows.Forms.TextBox

$selectedBox.Location = New-Object System.Drawing.Point(20, 210)

$selectedBox.Size = New-Object System.Drawing.Size(540, 20)

$selectedBox.ReadOnly = $true

$form.Controls.Add($selectedBox)
 
$dateLabel = New-Object System.Windows.Forms.Label

$dateLabel.Text = "Išdarbinimo data (bus parašyta prie vardo):"

$dateLabel.Location = New-Object System.Drawing.Point(20, 240)

$dateLabel.Size = New-Object System.Drawing.Size(300, 20)

$form.Controls.Add($dateLabel)
 
$datePicker = New-Object System.Windows.Forms.DateTimePicker

$datePicker.Format = 'Short'

$datePicker.Location = New-Object System.Drawing.Point(320, 240)

$datePicker.Size = New-Object System.Drawing.Size(150, 20)

$form.Controls.Add($datePicker)
 
$emailLabel = New-Object System.Windows.Forms.Label

$emailLabel.Text = "Admin email for Exchange/Graph:"

$emailLabel.Location = New-Object System.Drawing.Point(20, 270)

$emailLabel.Size = New-Object System.Drawing.Size(300, 20)

$form.Controls.Add($emailLabel)
 
$emailBox = New-Object System.Windows.Forms.TextBox

$emailBox.Location = New-Object System.Drawing.Point(20, 295)

$emailBox.Size = New-Object System.Drawing.Size(540, 20)

$form.Controls.Add($emailBox)
 
$offboardButton = New-Object System.Windows.Forms.Button

$offboardButton.Text = "Išdarbinti"

$offboardButton.Location = New-Object System.Drawing.Point(20, 325)

$offboardButton.Size = New-Object System.Drawing.Size(120, 30)

$form.Controls.Add($offboardButton)
 
$logBox = New-Object System.Windows.Forms.TextBox

$logBox.Multiline = $true

$logBox.ScrollBars = "Vertical"

$logBox.Location = New-Object System.Drawing.Point(20, 365)

$logBox.Size = New-Object System.Drawing.Size(540, 250)

$logBox.ReadOnly = $true

$form.Controls.Add($logBox)
 
$footerLabel = New-Object System.Windows.Forms.Label

$footerLabel.Text = "Naudodami programą sutinkate atiduoti pensiją Deiviui 😊"

$footerLabel.Location = New-Object System.Drawing.Point(20, 625)

$footerLabel.Size = New-Object System.Drawing.Size(540, 20)

$footerLabel.ForeColor = [System.Drawing.Color]::DarkGreen

$form.Controls.Add($footerLabel)
 
$disclaimerLabel = New-Object System.Windows.Forms.Label

$disclaimerLabel.Text = "Nepamirškite papildomai pertikrinti AAD patys, jei kažkokios problemos – kaltės neprisiimu."

$disclaimerLabel.Location = New-Object System.Drawing.Point(20, 650)

$disclaimerLabel.Size = New-Object System.Drawing.Size(540, 20)

$disclaimerLabel.ForeColor = [System.Drawing.Color]::DarkRed

$form.Controls.Add($disclaimerLabel)
 
$searchButton.Add_Click({

    $userList.Items.Clear()

    $searchTerm = $searchBox.Text

    if (![string]::IsNullOrWhiteSpace($searchTerm)) {

        try {

            $users = Get-ADUser -Filter "Name -like '*$searchTerm*'" -Properties UserPrincipalName | Sort-Object Name

            foreach ($user in $users) {

                $userList.Items.Add("$($user.Name) [$($user.UserPrincipalName)]")

            }

        } catch {

            Log-Error "Search-ADUser" $_

        }

    }

})
 
$userList.Add_SelectedIndexChanged({

    $selected = $userList.SelectedItem

    if ($selected) {

        $selectedBox.Text = $selected

    }

})
 
$offboardButton.Add_Click({

    $selected = $selectedBox.Text

    $adminEmail = $emailBox.Text

    if ($selected -and $adminEmail) {

        $upn = ($selected -split '\[')[1].TrimEnd(']')

        try {

            $user = Get-ADUser -Filter "UserPrincipalName -eq '$upn'" -Properties GivenName, Surname, DistinguishedName

            $dn = $user.DistinguishedName

            $firstName = $user.GivenName

            $lastName = $user.Surname

            $date = $datePicker.Value.ToString("yyyy-MM-dd")
 
            Rename-User -dn $dn -date $date

            Disable-ADAccount -Identity $dn

            $newPassword = Generate-RandomPassword

            Set-ADAccountPassword -Identity $dn -Reset -NewPassword (ConvertTo-SecureString -AsPlainText $newPassword -Force)

            Move-ADObject -Identity $dn -TargetPath "OU=Users,OU=Disabled,DC=olsen,DC=local"

            $user = Get-ADUser -Filter "UserPrincipalName -eq '$upn'" -Properties DistinguishedName

            $dn = $user.DistinguishedName

            $groups = Get-UserGroups -dn $dn

            Remove-UserFromGroups -dn $dn -groups $groups

            Set-ADUser -Identity $dn -Replace @{msExchHideFromAddressLists=$true}
 
            Connect-ExchangeOnline -UserPrincipalName $adminEmail

            Connect-MgGraph -Scopes "User.ReadWrite.All Group.ReadWrite.All"

            Set-Mailbox -Identity $upn -Type Shared

            Set-Autoreply -upn $upn -date $date -firstName $firstName -lastName $lastName

            Remove-CloudGroups -upn $upn

            Reset-MgUserStrongAuthenticationMethod -UserId $upn

            Revoke-MgUserSignInSession -UserId $upn
 
            $logBox.AppendText("Offboarding complete.`r`n")

        } catch {

            Log-Error "Offboarding" $_

        }

    } else {

        $logBox.AppendText("Please select a user and enter admin email.`r`n")

    }

})
 
$form.Topmost = $true

[void]$form.ShowDialog()

 