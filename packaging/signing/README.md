# Assinatura de código

Este projeto ainda não inclui certificado. Para builds de teste local:

```powershell
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=TMHue" `
  -KeyUsage DigitalSignature -FriendlyName "TMHue Dev" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

Export-Certificate -Cert $cert -FilePath TMHue.cer

signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a TMHue.exe
```

O certificado de desenvolvimento não deve ser instalado em `LocalMachine\Root` nem em
outro repositório do computador. Se for necessário validar localmente a confiança da
assinatura, faça isso apenas em uma VM de teste e no perfil do usuário:

```powershell
Import-Certificate -FilePath TMHue.cer -CertStoreLocation Cert:\CurrentUser\Root
Import-Certificate -FilePath TMHue.cer -CertStoreLocation Cert:\CurrentUser\TrustedPublisher
```

Remova os certificados após o teste. Nunca use este certificado para distribuição.

Para distribuição pública, use um certificado de assinatura de código de produção e mantenha
as credenciais fora do repositório. O comando de assinatura é informado ao script
`packaging/build-installer.ps1` e deve usar timestamp. Veja `docs/security.md`.
