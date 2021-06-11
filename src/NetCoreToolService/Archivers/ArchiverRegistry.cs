// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Steeltoe.NetCoreToolService.Services;
using System.Collections.Generic;

namespace Steeltoe.NetCoreToolService.Archivers
{
    /// <summary>
    /// An in-memory <see cref="IArchiverRegistry"/> implementation.
    /// </summary>
    public class ArchiverRegistry : Service, IArchiverRegistry
    {
        /* ----------------------------------------------------------------- *
         * fields                                                             *
         * ----------------------------------------------------------------- */

        private readonly Dictionary<string, IArchiver> _archivers = new Dictionary<string, IArchiver>();

        /* ----------------------------------------------------------------- *
         * constructors                                                      *
         * ----------------------------------------------------------------- */

        /// <summary>
        /// Initializes a new instance of the <see cref="ArchiverRegistry"/> class.
        /// </summary>
        /// <param name="logger">Injected logger.</param>
        public ArchiverRegistry(ILogger<ArchiverRegistry> logger)
            : base(logger)
        {
            Logger.LogInformation("Initializing archiver registry");
            Register(new ZipArchiver());
        }

        /* ----------------------------------------------------------------- *
         * methods                                                           *
         * ----------------------------------------------------------------- */

        /// <inheritdoc />
        public void Register(IArchiver archiver)
        {
            Logger.LogInformation(
                "Registering archiver: {Archiver} -> {ArchiverType}",
                archiver.Name,
                archiver.GetType());
            _archivers.Add(archiver.Name, archiver);
        }

        /// <inheritdoc />
        public IArchiver Lookup(string packaging)
        {
            _archivers.TryGetValue(packaging, out var archiver);
            return archiver;
        }
    }
}
