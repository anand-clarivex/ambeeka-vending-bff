/*
 ************************************************************************
 * Copyright (C) 2026 Clarivex Technologies Private Limited
 * All Rights Reserved.
 *
 * NOTICE: All intellectual and technical concepts contained
 * herein are proprietary to Clarivex Technologies Private Limited
 * and may be covered by Indian and Foreign Patents,
 * patents in process, and are protected by trade secret or
 * copyright law. Dissemination of this information or reproduction
 * of this material is strictly forbidden unless prior written
 * permission is obtained from Clarivex Technologies Private Limited.
 *
 * Product:   Pervaxis Platform
 * Website:   https://clarivex.tech
 ************************************************************************
 */

namespace Pervaxis.Vpa.Vending.Infrastructure.Exceptions;

/// <summary>
/// Thrown when a slug or unique identifier conflicts with an existing resource.
/// Maps to HTTP 409 Conflict.
/// </summary>
public sealed class SlugConflictException : Exception
{
    public SlugConflictException(string message) : base(message) { }

    public SlugConflictException(string message, Exception innerException) : base(message, innerException) { }
}
