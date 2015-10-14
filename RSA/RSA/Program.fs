open System.IO
open System.Text
open System.Security.Cryptography

let GenKey (pub : string) (pri : string) =
    let (rsa : RSACryptoServiceProvider) = new RSACryptoServiceProvider()
    File.WriteAllText(pub, rsa.ToXmlString(false));
    File.WriteAllText(pri, rsa.ToXmlString(true));

let EncryptMsg (pub : string) (input : string) (output : string) =
    let (rsa : RSACryptoServiceProvider) = new RSACryptoServiceProvider()
    rsa.FromXmlString(File.ReadAllText(pub))
    File.WriteAllBytes(output, rsa.Encrypt(File.ReadAllBytes(input), false))

let DecryptMsg (pri : string) (input : string) (output : string) =
    let (rsa : RSACryptoServiceProvider) = new RSACryptoServiceProvider()
    rsa.FromXmlString(File.ReadAllText(pri))
    File.WriteAllBytes(output, rsa.Decrypt(File.ReadAllBytes(input), false))

[<EntryPoint>]
let main argv = 
    match argv with
    | [| "genkey"; pub; pri |] -> GenKey pub pri
    | [| "encrypt"; pub; input; output |] -> EncryptMsg pub input output
    | [| "decrypt"; pri; input; output |] -> DecryptMsg pri input output
    | _ -> printfn """Synopsis:
RSA genkey  <output public  key file name> <output private key file name>
RSA encrypt <input  public  key file name> <input file to encrypt> <output encrypted file>
RSA decrypt <input  private key file name> <input file to decrypt> <output decrypted file>
"""
    0
