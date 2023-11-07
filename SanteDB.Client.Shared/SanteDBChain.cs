using SanteDB.Core.Security.Certs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Client.Shared
{
    /// <summary>
    /// Specialized <see cref="X509Chain"/> like class for embedded certificates. It can be used on platforms where third party certification authorities cannot be used.
    /// </summary>
    public class SanteDBChain : IDisposable
    {
        readonly List<X509Certificate2> _TrustedCertificates;
        readonly List<X509Certificate2> _ValidatedAuthorities;

        /// <summary>
        /// Initializes a new instance of the class and loads the trust anchors from the asemblies.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public SanteDBChain()
        {
            var thisassembly = typeof(ExtendedKeyUsageOids)?.Assembly; //typeof(SanteDBChain)?.Assembly;

            if (null == thisassembly)
            {
                throw new InvalidOperationException("Reflection is not available on this platform.");
            }

            //We can "fully" trust these certificates. There is no way to verify that our assembly has not been tampered if the assembly is signed with these certificates. Therefore, this is best effort :|
            var certificates = thisassembly?.GetManifestResourceNames()?.Where(res => res.EndsWith(".cer", StringComparison.InvariantCultureIgnoreCase) || res.EndsWith(".crt", StringComparison.InvariantCultureIgnoreCase));
            var trustcerts = certificates?.Where(c => c.Contains(".trust.", StringComparison.InvariantCultureIgnoreCase));
            var intercerts = certificates?.Where(c => c.Contains(".inter.", StringComparison.InvariantCultureIgnoreCase));

            _ValidatedAuthorities = new();
            _TrustedCertificates = new();

            if (null == trustcerts)
            {
                throw new InvalidOperationException("No root authorities were found.");
            }

            foreach (var certname in trustcerts)
            {
                var cert = LoadCertificate(thisassembly!, certname);

                if (null != cert && ValidateRootAuthority(cert))
                {
                    _ValidatedAuthorities.Add(cert);
                }
            }

            if (null != intercerts)
            {
                var failedintercerts = new List<X509Certificate2>();

                foreach (var certname in intercerts)
                {
                    var cert = LoadCertificate(thisassembly!, certname);

                    if (null == cert)
                        continue;

                    if (ValidateIntermediateAuthority(cert, _ValidatedAuthorities))
                    {
                        _ValidatedAuthorities.Add(cert);
                    }
                    else
                    {
                        failedintercerts.Add(cert);
                    }
                }

                int retrycount = 0;

                while (failedintercerts.Count > 1 && retrycount < 5)
                {
                    var interretry = failedintercerts.ToList();
                    failedintercerts.Clear();

                    foreach (var cert in interretry)
                    {
                        if (ValidateIntermediateAuthority(cert, _ValidatedAuthorities))
                        {
                            _ValidatedAuthorities.Add(cert);
                        }
                        else
                        {
                            failedintercerts.Add(cert);
                        }
                    }

                    retrycount++;
                }
            }

        }

        /// <summary>
        /// Loads a certificate from the assembly using the resource name.
        /// </summary>
        /// <param name="assembly">The assembly to load the certificate from.</param>
        /// <param name="resourceName">The resource name used to load the certificate.</param>
        /// <returns></returns>
        private static X509Certificate2? LoadCertificate(Assembly assembly, string resourceName)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream?.CanSeek == true)
            {
                using var binreader = new BinaryReader(stream);
                var bytes = binreader.ReadBytes((int)stream.Length);

                return new X509Certificate2(bytes);
            }
            else if (stream != null)
            {
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return new X509Certificate2(buffer.ToArray());
            }

            return null;
        }

        private static bool ValidateRootAuthority(X509Certificate2 certificate)
        {
            if (null == certificate)
                return false;

            //First check the subject and issuer match
            var issuer = certificate.IssuerName.Decode(X500DistinguishedNameFlags.None);
            var subject = certificate.SubjectName.Decode(X500DistinguishedNameFlags.None);

            if (null == issuer || null == subject)
                return false;

            if (!subject.Equals(issuer, StringComparison.Ordinal))
                return false;

            //Then check for extensions for subject key identifier/authority key identifier
            var ski = certificate.Extensions.OfType<X509SubjectKeyIdentifierExtension>().FirstOrDefault();
            var aki = certificate.Extensions.OfType<X509AuthorityKeyIdentifierExtension>().FirstOrDefault();

            if (null != aki)
            {
                if (aki.KeyIdentifier != null)
                {
                    if (null == ski) //Can't have Authority without subject on root.
                        return false;

                    if (!aki.KeyIdentifier.Value.Span.SequenceEqual(ski.SubjectKeyIdentifierBytes.Span))
                        return false;
                }
                else if (aki.NamedIssuer != null)
                {
                    var authority = aki.NamedIssuer!.Decode(X500DistinguishedNameFlags.None);

                    if (!subject.Equals(authority))
                        return false;
                }
                else if (aki.SerialNumber != null)
                {
                    if (!certificate.SerialNumberBytes.Span.SequenceEqual(aki.SerialNumber.Value.Span))
                        return false;
                }
                else //Unrecognized format for alternate key identifier.
                {
                    return false;
                }
            }

            //Now validate basic constraints
            var basicconstraints = certificate.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault();

            if (null == basicconstraints)
                return false;

            if (!basicconstraints.CertificateAuthority)
                return false;

            if (basicconstraints.HasPathLengthConstraint && basicconstraints.PathLengthConstraint < 0)
                return false;

            //var now = DateTime.Now;

            //if (now < certificate.NotBefore)
            //    return false;

            //if (now > certificate.NotAfter)
            //    return false;

            //All checks passed.
            return true;

        }

        private static X509Certificate2? FindIssuer(X509Certificate2 certificate, IReadOnlyList<X509Certificate2> authorities)
        {
            var aki = certificate.Extensions.OfType<X509AuthorityKeyIdentifierExtension>().FirstOrDefault();
            X509Certificate2? issuer = null;

            if (null != aki)
            {
                if (aki.KeyIdentifier != null)
                {
                    issuer = authorities.FirstOrDefault(a =>
                        (a.Extensions.OfType<X509SubjectKeyIdentifierExtension>()?.FirstOrDefault()?.SubjectKeyIdentifierBytes.Span.SequenceEqual(aki.KeyIdentifier.Value.Span) ?? false)
                        && a.NotBefore <= certificate.NotBefore
                        && a.NotAfter >= certificate.NotAfter
                    );
                }
                else if (aki.SerialNumber != null)
                {
                    issuer = authorities.FirstOrDefault(a =>
                        a.SerialNumberBytes.Span.SequenceEqual(aki.SerialNumber.Value.Span)
                        && a.NotBefore <= certificate.NotBefore
                        && a.NotAfter >= certificate.NotAfter
                    );
                }
                else if (aki.NamedIssuer != null)
                {
                    var issuername = aki.NamedIssuer.Decode(X500DistinguishedNameFlags.None);
                    issuer = authorities.FirstOrDefault(a =>
                        issuername.Equals(a.SubjectName.Decode(X500DistinguishedNameFlags.None))
                        && a.NotBefore <= certificate.NotBefore
                        && a.NotAfter >= certificate.NotAfter
                    );
                }
                else
                {
                    return null; //Unrecognized format for alternate key identifier.
                }


            }
            else //TODO: We should probably disable this due to the number of issues around finding by "Issuer".
            {
                //Search by issuer.
                var issuername = certificate.IssuerName.Decode(X500DistinguishedNameFlags.None);
                issuer = authorities.FirstOrDefault(a =>
                    a.SubjectName.Decode(X500DistinguishedNameFlags.None).Equals(issuername)
                    && a.NotBefore <= certificate.NotBefore
                    && a.NotAfter >= certificate.NotAfter);
            }

            return issuer;
        }

        private static bool IsIssuerExtendedUsagesValid(X509Certificate2 certificate, X509Certificate2 issuer)
        {
            var issuereku = issuer.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();

            if (null != issuereku && issuereku.EnhancedKeyUsages.OfType<Oid>().Any(o => o.Value == ExtendedKeyUsageOids.AllIssuancePolicy) != true)
            {
                //We need to do an intersection check for the issuance policies from the issuer to us.
                //If this was true, it means the issuer can issue for any policy and we didn't
                //need to do any checks

                var certeku = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>()?.FirstOrDefault();

                if (null != certeku && null != issuereku)
                {
                    var issueroids = issuereku.EnhancedKeyUsages.OfType<Oid>().Where(o => null != o).Select(o => o.Value).ToList();

                    foreach (var oid in certeku.EnhancedKeyUsages)
                    {
                        if (null == oid)
                        {
                            continue;
                        }

                        if (!issueroids.Contains(oid.Value)) //Key usage not found in issuer.
                        {
                            return false;
                        }
                    }
                }

            }

            return true;
        }

        private static bool ValidateIntermediateAuthority(X509Certificate2 certificate, IReadOnlyList<X509Certificate2> authorities)
        {
            if (null == certificate)
                return false;

            var issuer = FindIssuer(certificate, authorities);

            if (null == issuer) //If we couldn't find the issuer cert then it's invalid.
            {
                return false;
            }

            //Ignore NameConstraints Extension
            if (!IsIssuerExtendedUsagesValid(certificate, issuer))
            {
                return false;
            }

            var basicconstraints = certificate.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault();

            if (null == basicconstraints)
                return false;

            if (!basicconstraints.CertificateAuthority)
                return false;

            if (basicconstraints.HasPathLengthConstraint && basicconstraints.PathLengthConstraint < 0)
                return false;

            return true;
        }

        private static bool ValidateEndEntityCertificate(X509Certificate2 certificate, IReadOnlyList<X509Certificate2> authorities)
        {
            if (null == certificate)
                return false;

          

            X509Certificate2? issuer = FindIssuer(certificate, authorities);

            if (null == issuer)
            {
                return false;
            }

            if (!IsIssuerExtendedUsagesValid(certificate, issuer))
            {
                return false;
            }

            //Inspect the basic constraints
            var basicconstraints = certificate.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault();

            if (null == basicconstraints)
            {
                return false;
            }

            if (basicconstraints.CertificateAuthority == true || basicconstraints.HasPathLengthConstraint == true)
            {
                return false;
            }

            //TODO: Validate key usage.

            return true;
        }

        /// <summary>
        /// Validate a certificate is trusted by a built-in trust anchor.
        /// </summary>
        /// <param name="certificate">The certificate to validate.</param>
        /// <param name="asOfDate">An optional date to validate the certificate for. This is typically used for durable assets which are signed at a particular date.</param>
        /// <returns><c>true</c> if the certificate is valid, <c>false</c> otherwise.</returns>
        public bool ValidateCertificate(X509Certificate2 certificate, DateTimeOffset? asOfDate = null)
        {
            if (null == certificate)
                return false;

            if (!_TrustedCertificates.Contains(certificate))
            {
                if (!ValidateEndEntityCertificate(certificate, _ValidatedAuthorities))
                {
                    return false;
                }

                _TrustedCertificates.Add(certificate);
            }

            if (null != asOfDate)
            {
                asOfDate = DateTimeOffset.UtcNow;
            }

            if (certificate.NotBefore > asOfDate || certificate.NotAfter < asOfDate)
            {
                return false;
            }

            return true;
        }



        #region Disposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        foreach (var cert in _TrustedCertificates)
                        {
                            try
                            {
                                cert?.Dispose();
                            }
                            catch (ObjectDisposedException) { }
                        }
                    }
                    catch (InvalidOperationException) { } //Collection was modified because Dispose was called from multiple threads

                    try
                    {
                        foreach (var cert in _ValidatedAuthorities)
                        {
                            try
                            {
                                cert?.Dispose();
                            }
                            catch (ObjectDisposedException) { }
                        }
                    }
                    catch (InvalidOperationException) { } //Collection was modified because Dispose was called from multiple threads

                    _TrustedCertificates.Clear();
                    _ValidatedAuthorities.Clear();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
