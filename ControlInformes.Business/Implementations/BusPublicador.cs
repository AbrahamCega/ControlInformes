using AutoMapper;
using ControlInformes.Business.DTOs;
using ControlInformes.Business.Interfaces;
using ControlInformes.Data.Interfaces;
using ControlInformes.Domain.Entities;
using ControlInformes.Domain.Enums;
using ControlInformes.Utils;
using iText.Forms;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.IO.Compression;

namespace ControlInformes.Business.Implementations;

public class BusPublicador : IBusPublicador
{
    private readonly IDatPublicador _datPublicador;
    private readonly IDatInformeMensual _datInforme;
    private readonly IMapper _mapper;
    private readonly ILogger<BusPublicador> _logger;

    private static readonly string[] _nombresMeses =
    [
        "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    ];

    public BusPublicador(
        IDatPublicador datPublicador,
        IDatInformeMensual datInforme,
        IMapper mapper,
        ILogger<BusPublicador> logger)
    {
        _datPublicador = datPublicador;
        _datInforme = datInforme;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ApiResponse<List<PublicadorDto>>> GetAllAsync()
    {
        try
        {
            var publicadores = await _datPublicador.GetAllAsync();
            var result = _mapper.Map<List<PublicadorDto>>(publicadores);
            return ApiResponse<List<PublicadorDto>>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener publicadores.");
            return ApiResponse<List<PublicadorDto>>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }

    public async Task<ApiResponse<PublicadorDto>> GetByIdAsync(Guid id)
    {
        try
        {
            var publicador = await _datPublicador.GetByIdAsync(id);
            if (publicador == null)
                return ApiResponse<PublicadorDto>.NotFound(
                    $"Publicador con Id ({id}) no encontrado.", ErrorCatalog.EntidadNoEncontrada);

            var result = _mapper.Map<PublicadorDto>(publicador);
            return ApiResponse<PublicadorDto>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener publicador por Id: {Id}.", id);
            return ApiResponse<PublicadorDto>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }

    public async Task<ApiResponse<Guid>> CrearAsync(CrearPublicadorDto dto)
    {
        try
        {
            var erroresRol = ValidarRol(dto.Tipo, dto.Rol);
            if (erroresRol.Count > 0)
                return ApiResponse<Guid>.Fail(
                    "Errores de validación.", ErrorCatalog.ValidacionFallida, 400, erroresRol);

            var publicador = _mapper.Map<Publicador>(dto);
            publicador.IdPublicador = Guid.NewGuid();
            publicador.Activo = true;
            publicador.FechaCreacion = DateTime.Now;

            await _datPublicador.AddAsync(publicador);
            await _datPublicador.SaveChangesAsync();

            _logger.LogInformation("Publicador creado: {Id} - {Nombre}",
                publicador.IdPublicador, publicador.NombreCompleto);

            return ApiResponse<Guid>.Ok(publicador.IdPublicador, "Publicador creado.", 201);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear publicador.");
            return ApiResponse<Guid>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }

    public async Task<ApiResponse<string>> ActualizarAsync(ActualizarPublicadorDto dto)
    {
        try
        {
            var publicador = await _datPublicador.GetByIdAsync(dto.IdPublicador);
            if (publicador == null)
                return ApiResponse<string>.NotFound(
                    $"Publicador con Id ({dto.IdPublicador}) no encontrado.",
                    ErrorCatalog.EntidadNoEncontrada);

            var erroresRol = ValidarRol(dto.Tipo, dto.Rol);
            if (erroresRol.Count > 0)
                return ApiResponse<string>.Fail(
                    "Errores de validación.", ErrorCatalog.ValidacionFallida, 400, erroresRol);

            publicador.NombreCompleto = dto.NombreCompleto;
            publicador.FechaNacimiento = dto.FechaNacimiento;
            publicador.FechaBautismo = dto.FechaBautismo;
            publicador.Genero = dto.Genero;
            publicador.CondicionEspiritual = dto.CondicionEspiritual;
            publicador.Tipo = dto.Tipo;
            publicador.Rol = dto.Rol;
            publicador.IdGrupo = dto.IdGrupo;
            publicador.Inactivo = dto.Inactivo;
            publicador.Activo = true;

            _datPublicador.Update(publicador);
            await _datPublicador.SaveChangesAsync();

            _logger.LogInformation("Publicador actualizado: {Id}", dto.IdPublicador);
            return ApiResponse<string>.Ok("Actualizado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar publicador: {Id}.", dto.IdPublicador);
            return ApiResponse<string>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }

    public async Task<ApiResponse<string>> EliminarAsync(Guid id)
    {
        try
        {
            var publicador = await _datPublicador.GetByIdAsync(id);
            if (publicador == null)
                return ApiResponse<string>.NotFound(
                    $"Publicador con Id ({id}) no encontrado.", ErrorCatalog.EntidadNoEncontrada);

            _datPublicador.Delete(publicador);
            await _datPublicador.SaveChangesAsync();

            _logger.LogInformation("Publicador eliminado: {Id}", id);
            return ApiResponse<string>.Ok("Eliminado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar publicador: {Id}.", id);
            return ApiResponse<string>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }

    public async Task<ApiResponse<TarjetaPublicadorDto>> GetTarjetaAsync(Guid idPublicador, int? anoServicio)
    {
        try
        {
            var publicador = await _datPublicador.GetByIdAsync(idPublicador);
            if (publicador == null)
                return ApiResponse<TarjetaPublicadorDto>.NotFound(
                    $"Publicador con Id ({idPublicador}) no encontrado.",
                    ErrorCatalog.EntidadNoEncontrada);

            // ── Año de servicio ───────────────────────────────────────
            // El año de servicio 2026 = Sep 2025 → Ago 2026
            var now = DateTime.Now;
            int anoServicioActual = anoServicio ?? (now.Month >= 9 ? now.Year + 1 : now.Year);
            int anioSepDic = anoServicioActual - 1; // Sep–Dic caen en el año civil anterior
            int anioEneAgo = anoServicioActual;     // Ene–Ago caen en el año civil del año de servicio

            // ── Informes del publicador ───────────────────────────────
            var informes = await _datInforme.GetByPublicadorAsync(idPublicador);

            var meses = new List<TarjetaMesDto>();

            var mesesConfig = new[]
            {
            (9,  anioSepDic), (10, anioSepDic), (11, anioSepDic), (12, anioSepDic),
            (1,  anioEneAgo), (2,  anioEneAgo), (3,  anioEneAgo), (4,  anioEneAgo),
            (5,  anioEneAgo), (6,  anioEneAgo), (7,  anioEneAgo), (8,  anioEneAgo)
        };

            foreach (var (mes, ano) in mesesConfig)
            {
                var inf = informes.FirstOrDefault(i => i.Ano == ano && i.Mes == mes);
                meses.Add(MapMes(ano, mes, inf));
            }

            // ── Armar DTO ─────────────────────────────────────────────
            var tarjeta = new TarjetaPublicadorDto
            {
                IdPublicador = publicador.IdPublicador,
                NombreCompleto = publicador.NombreCompleto,
                FechaNacimiento = publicador.FechaNacimiento,
                FechaBautismo = publicador.FechaBautismo,
                FechaNacimientoFormateada = publicador.FechaNacimiento?.ToString("dd-MM-yyyy"),
                FechaBautismoFormateada = publicador.FechaBautismo?.ToString("dd-MM-yyyy"),
                Genero = publicador.Genero,
                GeneroDescripcion = publicador.Genero.ToString(),
                CondicionEspiritual = publicador.CondicionEspiritual,
                CondicionEspiritualDescripcion = publicador.CondicionEspiritual.ToString(),
                Tipo = publicador.Tipo,
                TipoDescripcion = publicador.Tipo.ToString(),
                Rol = publicador.Rol,
                RolDescripcion = publicador.Rol.ToString(),
                NombreGrupo = publicador.Grupo?.Nombre ?? string.Empty,
                AnoServicioInicio = anoServicioActual,
                AnoServicioFin = anioEneAgo,
                Meses = meses
            };

            return ApiResponse<TarjetaPublicadorDto>.Ok(tarjeta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener tarjeta del publicador: {Id}.", idPublicador);
            return ApiResponse<TarjetaPublicadorDto>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }

    public async Task<ApiResponse<List<PublicadorDto>>> GetSinGrupoAsync()
    {
        try
        {
            var publicadores = await _datPublicador.GetSinGrupoAsync();
            var result = _mapper.Map<List<PublicadorDto>>(publicadores);
            return ApiResponse<List<PublicadorDto>>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener publicadores sin grupo.");
            return ApiResponse<List<PublicadorDto>>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }

    public async Task<ApiResponse<PagedResult<PublicadorGrupoDto>>> GetListadoPaginadoAsync(FiltroPublicadorGrupoDto filtro)
    {
        try
        {
            var (items, total) = await _datPublicador.GetPaginadoConGrupoAsync(
                filtro.IdGrupo,
                filtro.IdPublicador,
                filtro.NombreCompleto,
                filtro.Tipo,
                filtro.Inactivo,
                filtro.Pagina,
                filtro.TamanoPagina);

            var dtos = items.Select(p => new PublicadorGrupoDto
            {
                IdPublicador = p.IdPublicador,
                NombrePublicador = p.NombreCompleto,
                Tipo = (int)p.Tipo,
                TipoDescripcion = p.Tipo.ToString(),
                IdGrupo = p.IdGrupo,
                NombreGrupo = p.Grupo?.Nombre ?? string.Empty,
                EsCapitan = p.Grupo?.IdCapitan == p.IdPublicador,
                Inactivo = p.Inactivo
            }).ToList();

            var result = new PagedResult<PublicadorGrupoDto>
            {
                Items = dtos,
                TotalRegistros = total,
                Pagina = filtro.Pagina,
                TamanoPagina = filtro.TamanoPagina
            };

            return ApiResponse<PagedResult<PublicadorGrupoDto>>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener listado paginado de publicadores por grupo.");
            return ApiResponse<PagedResult<PublicadorGrupoDto>>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }

    public async Task<ApiResponse<byte[]>> DescargarTarjetaPdfAsync(Guid idPublicador, int? anoServicio)
    {
        try
        {
            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Template", "Template_Tarjeta_Publicador.pdf")))
                return ApiResponse<byte[]>.Error(
                    "Template de tarjeta no encontrado.", ErrorCatalog.ArchivoInvalido);

            var tarjetaResponse = await GetTarjetaAsync(idPublicador, anoServicio);
            if (tarjetaResponse.HasError)
                return ApiResponse<byte[]>.Error(tarjetaResponse.Mensaje, tarjetaResponse.CodigoError);

            var bytes = GenerarPdfTarjeta(tarjetaResponse.Result!);
            return ApiResponse<byte[]>.Ok(bytes, "PDF generado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar PDF de tarjeta: {Id}.", idPublicador);
            return ApiResponse<byte[]>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }

    public async Task<ApiResponse<byte[]>> DescargarTarjetasPorGrupoAsync(Guid idGrupo, int? anoServicio)
    {
        try
        {
            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Template", "Template_Tarjeta_Publicador.pdf")))
                return ApiResponse<byte[]>.Error(
                    "Template de tarjeta no encontrado.", ErrorCatalog.ArchivoInvalido);

            var publicadores = await _datPublicador.GetByGrupoAsync(idGrupo);
            if (!publicadores.Any())
                return ApiResponse<byte[]>.NotFound(
                    $"No se encontraron publicadores en el grupo con Id ({idGrupo}).",
                    ErrorCatalog.EntidadNoEncontrada);

            var now = DateTime.Now;
            int anoInicio = anoServicio ?? (now.Month >= 9 ? now.Year : now.Year - 1);
            int anoFin = anoInicio + 1;
            string periodo = $"{anoInicio}-{anoFin}";

            using var msZip = new MemoryStream();
            using var zipArchive = new ZipArchive(msZip, ZipArchiveMode.Create, leaveOpen: true);

            foreach (var publicador in publicadores.OrderBy(p => p.NombreCompleto))
            {
                var tarjetaResponse = await GetTarjetaAsync(publicador.IdPublicador, anoServicio);
                if (tarjetaResponse.HasError) continue;

                var bytesIndividual = GenerarPdfTarjeta(tarjetaResponse.Result!);
                var nombreArchivo = $"{publicador.NombreCompleto} - {periodo}.pdf";

                foreach (char c in Path.GetInvalidFileNameChars())
                    nombreArchivo = nombreArchivo.Replace(c, '_');

                var entrada = zipArchive.CreateEntry(nombreArchivo, CompressionLevel.Fastest);
                using var entradaStream = entrada.Open();
                await entradaStream.WriteAsync(bytesIndividual);
            }

            zipArchive.Dispose();
            return ApiResponse<byte[]>.Ok(msZip.ToArray(), "ZIP generado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar tarjetas del grupo: {Id}.", idGrupo);
            return ApiResponse<byte[]>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }

    public async Task<ApiResponse<ResultadoImportacionTarjetasDto>> ImportarTarjetasAsync(
    List<IFormFile> archivos, Guid? idGrupo)
    {
        try
        {
            var resultado = new ResultadoImportacionTarjetasDto();

            foreach (var archivo in archivos)
            {
                try
                {
                    using var stream = archivo.OpenReadStream();
                    using var msTemp = new MemoryStream();
                    await stream.CopyToAsync(msTemp);
                    msTemp.Position = 0;

                    using var reader = new PdfReader(msTemp);
                    using var pdfDoc = new PdfDocument(reader);
                    var form = PdfAcroForm.GetAcroForm(pdfDoc, false);

                    if (form == null)
                    {
                        resultado.Errores.Add($"{archivo.FileName}: No se encontraron campos de formulario.");
                        resultado.Fallidos++;
                        continue;
                    }

                    var camposActivos = form.GetAllFormFields();
                    if (camposActivos.Count == 0)
                    {
                        resultado.Errores.Add($"{archivo.FileName}: El PDF está aplanado y no puede reimportarse. Descarga una nueva versión.");
                        resultado.Fallidos++;
                        continue;
                    }

                    // ── Leer datos del publicador ─────────────────────────────
                    var nombre = GetCampo(form, "900_1_Text_SanSerif");
                    var fechaNacStr = GetCampo(form, "900_2_Text_SanSerif");
                    var fechaBautStr = GetCampo(form, "900_5_Text_SanSerif");
                    var anoServicioStr = GetCampo(form, "900_13_Text_C_SanSerif");

                    if (string.IsNullOrWhiteSpace(nombre))
                    {
                        resultado.Errores.Add($"{archivo.FileName}: El campo Nombre está vacío.");
                        resultado.Fallidos++;
                        continue;
                    }

                    // ── Parsear fechas ────────────────────────────────────────
                    DateTime? fechaNac = ParseFecha(fechaNacStr);
                    DateTime? fechaBaut = ParseFecha(fechaBautStr);

                    // ── Género ────────────────────────────────────────────────
                    var esMujer = GetCheckbox(form, "900_4_CheckBox");
                    var genero = esMujer ? Genero.Mujer : Genero.Hombre;

                    // ── Condición espiritual ──────────────────────────────────
                    var esUngido = GetCheckbox(form, "900_7_CheckBox");
                    var condicionEspiritual = esUngido
                        ? CondicionEspiritual.Ungido
                        : CondicionEspiritual.OtrasOvejas;

                    // ── Tipo de publicador ────────────────────────────────────
                    var esPrecursorRegular = GetCheckbox(form, "900_10_CheckBox");
                    var tipo = esPrecursorRegular
                        ? TipoPublicador.PrecursorRegular
                        : fechaBaut == null
                            ? TipoPublicador.NoBautizado
                            : TipoPublicador.Publicador;

                    // ── Rol ───────────────────────────────────────────────────
                    var esAnciano = GetCheckbox(form, "900_8_CheckBox");
                    var esSiervoMinisterial = GetCheckbox(form, "900_9_CheckBox");
                    var rol = esAnciano
                        ? RolCongregacion.Anciano
                        : esSiervoMinisterial
                            ? RolCongregacion.SiervoMinisterial
                            : RolCongregacion.Ninguno;

                    // ── Año de servicio ───────────────────────────────────────
                    // El año de servicio 2026 = Sep 2025 → Ago 2026
                    if (!int.TryParse(anoServicioStr?.Trim(), out int anoServicio))
                    {
                        var now = DateTime.Now;
                        // Si estamos en Sep o después, el año de servicio en curso es el siguiente año civil
                        anoServicio = now.Month >= 9 ? now.Year + 1 : now.Year;
                    }

                    int anioSepDic = anoServicio - 1; // Sep–Dic caen en el año civil anterior
                    int anioEnéAgo = anoServicio;     // Ene–Ago caen en el año civil del año de servicio

                    // ── Upsert publicador ─────────────────────────────────────
                    var publicadorExistente = await _datPublicador.GetByNombreAsync(nombre.Trim());

                    if (publicadorExistente != null)
                    {
                        publicadorExistente.FechaNacimiento = fechaNac ?? publicadorExistente.FechaNacimiento;
                        publicadorExistente.FechaBautismo = fechaBaut ?? publicadorExistente.FechaBautismo;
                        publicadorExistente.Genero = genero;
                        publicadorExistente.CondicionEspiritual = condicionEspiritual;
                        publicadorExistente.Tipo = tipo;
                        publicadorExistente.Rol = rol;
                        publicadorExistente.IdGrupo = idGrupo ?? publicadorExistente.IdGrupo;
                        _datPublicador.Update(publicadorExistente);
                        resultado.Actualizados.Add(nombre);
                    }
                    else
                    {
                        var nuevoPublicador = new Publicador
                        {
                            IdPublicador = Guid.NewGuid(),
                            NombreCompleto = nombre.Trim(),
                            FechaNacimiento = fechaNac,
                            FechaBautismo = fechaBaut,
                            Genero = genero,
                            CondicionEspiritual = condicionEspiritual,
                            Tipo = tipo,
                            Rol = rol,
                            IdGrupo = idGrupo,
                            Activo = true,
                            Inactivo = false,
                            FechaCreacion = DateTime.Now
                        };
                        await _datPublicador.AddAsync(nuevoPublicador);
                        publicadorExistente = nuevoPublicador;
                        resultado.Creados.Add(nombre);
                    }

                    await _datPublicador.SaveChangesAsync();

                    // ── Leer e importar informes mensuales ────────────────────
                    // (campoBase, mes, año)
                    // Sep–Dic → anioSepDic (ej. 2025 para año de servicio 2026)
                    // Ene–Ago → anioEnéAgo (ej. 2026 para año de servicio 2026)
                    var mesesConfig = new[]
                    {
                    (20, 9,  anioSepDic),
                    (21, 10, anioSepDic),
                    (22, 11, anioSepDic),
                    (23, 12, anioSepDic),
                    (24, 1,  anioEnéAgo),
                    (25, 2,  anioEnéAgo),
                    (26, 3,  anioEnéAgo),
                    (27, 4,  anioEnéAgo),
                    (28, 5,  anioEnéAgo),
                    (29, 6,  anioEnéAgo),
                    (30, 7,  anioEnéAgo),
                    (31, 8,  anioEnéAgo)
                };

                    var nuevosInformes = new List<InformeMensual>();

                    foreach (var (numero, mes, ano) in mesesConfig)
                    {
                        var participo = GetCheckbox(form, $"901_{numero}_CheckBox");
                        var precursorAux = GetCheckbox(form, $"903_{numero}_CheckBox");
                        var cursosStr = GetCampo(form, $"902_{numero}_Text_C_SanSerif");
                        var horasStr = GetCampo(form, $"904_{numero}_S21_Value");
                        var notas = GetCampo(form, $"905_{numero}_Text_SanSerif");

                        int.TryParse(cursosStr, out int cursos);
                        int? horas = int.TryParse(horasStr, out int h) ? h : null;

                        var tipoMes = precursorAux
                            ? TipoPublicador.PrecursorAuxiliar
                            : tipo;

                        var (horasLimpias, cursosLimpios) = LimpiarCamposPorTipo(
                            tipoMes, false, participo, horas, cursos);

                        var existente = await _datInforme.GetByPublicadorMesAsync(
                            publicadorExistente.IdPublicador, ano, mes);

                        if (existente != null)
                        {
                            existente.Participo = participo;
                            existente.CursosBiblicos = cursosLimpios;
                            existente.Horas = horasLimpias;
                            existente.Tipo = tipoMes;
                            existente.Inactivo = false;
                            existente.Observacion = string.IsNullOrWhiteSpace(notas) ? null : notas;
                            _datInforme.Update(existente);
                        }
                        else if (participo || cursos > 0 || horas.HasValue)
                        {
                            nuevosInformes.Add(new InformeMensual
                            {
                                IdInformeMensual = Guid.NewGuid(),
                                IdPublicador = publicadorExistente.IdPublicador,
                                Ano = ano,
                                Mes = mes,
                                Participo = participo,
                                CursosBiblicos = cursosLimpios,
                                Horas = horasLimpias,
                                Tipo = tipoMes,
                                Inactivo = false,
                                Observacion = string.IsNullOrWhiteSpace(notas) ? null : notas
                            });
                        }
                    }

                    if (nuevosInformes.Any())
                        await _datInforme.AddRangeAsync(nuevosInformes);

                    await _datInforme.SaveChangesAsync();
                    resultado.Exitosos++;
                }
                catch (Exception ex)
                {
                    resultado.Errores.Add($"{archivo.FileName}: {ex.Message}");
                    resultado.Fallidos++;
                }
            }

            _logger.LogInformation("Importación tarjetas: {Exitosos} exitosos, {Fallidos} fallidos.",
                resultado.Exitosos, resultado.Fallidos);

            return ApiResponse<ResultadoImportacionTarjetasDto>.Ok(resultado, "Importación completada.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al importar tarjetas PDF.");
            return ApiResponse<ResultadoImportacionTarjetasDto>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }

    // ── Helpers privados ─────────────────────────────────────────────────────

    private static List<string> ValidarRol(TipoPublicador tipo, RolCongregacion rol)
    {
        var errores = new List<string>();
        if (tipo == TipoPublicador.NoBautizado && rol != RolCongregacion.Ninguno)
            errores.Add("Un publicador no bautizado no puede tener rol de Anciano o Siervo Ministerial.");
        return errores;
    }

    private static TarjetaMesDto MapMes(int ano, int mes, InformeMensual? informe)
    {
        return new TarjetaMesDto
        {
            Ano = ano,
            Mes = mes,
            NombreMes = _nombresMeses[mes],
            Participo = informe?.Participo ?? false,
            CursosBiblicos = informe?.CursosBiblicos ?? 0,
            Horas = informe?.Horas,
            PrecursorAuxiliar = informe?.Tipo == TipoPublicador.PrecursorAuxiliar,
            Notas = informe == null ? "Sin informe" : null
        };
    }

    private static byte[] GenerarPdfTarjeta(TarjetaPublicadorDto tarjeta)
    {
        using var ms = new MemoryStream();
        using var reader = new PdfReader(
            Path.Combine(AppContext.BaseDirectory, "Template", "Template_Tarjeta_Publicador.pdf"));
        using var writer = new PdfWriter(ms);
        using var pdfDoc = new PdfDocument(reader, writer);
        var form = PdfAcroForm.GetAcroForm(pdfDoc, false);

        SetCampo(form, "900_1_Text_SanSerif", tarjeta.NombreCompleto);
        SetCampo(form, "900_2_Text_SanSerif", tarjeta.FechaNacimiento?.ToString("dd-MM-yyyy") ?? string.Empty);
        SetCampo(form, "900_5_Text_SanSerif", tarjeta.FechaBautismo?.ToString("dd-MM-yyyy") ?? string.Empty);
        SetCampo(form, "900_13_Text_C_SanSerif", tarjeta.AnoServicioInicio.ToString());

        SetCheckbox(form, "900_3_CheckBox", tarjeta.Genero == Genero.Hombre);
        SetCheckbox(form, "900_4_CheckBox", tarjeta.Genero == Genero.Mujer);

        SetCheckbox(form, "900_6_CheckBox", tarjeta.CondicionEspiritual == CondicionEspiritual.OtrasOvejas);
        SetCheckbox(form, "900_7_CheckBox", tarjeta.CondicionEspiritual == CondicionEspiritual.Ungido);

        SetCheckbox(form, "900_8_CheckBox", tarjeta.Rol == RolCongregacion.Anciano);
        SetCheckbox(form, "900_9_CheckBox", tarjeta.Rol == RolCongregacion.SiervoMinisterial);
        SetCheckbox(form, "900_10_CheckBox", tarjeta.Tipo == TipoPublicador.PrecursorRegular);
        SetCheckbox(form, "900_11_CheckBox", false);
        SetCheckbox(form, "900_12_CheckBox", false);

        for (int i = 0; i < tarjeta.Meses.Count && i < 12; i++)
        {
            var mes = tarjeta.Meses[i];
            int numero = 20 + i;

            SetCheckbox(form, $"901_{numero}_CheckBox", mes.Participo);
            SetCampo(form, $"902_{numero}_Text_C_SanSerif", mes.Participo && mes.CursosBiblicos > 0
                ? mes.CursosBiblicos.ToString() : string.Empty);
            SetCheckbox(form, $"903_{numero}_CheckBox", mes.PrecursorAuxiliar);
            SetCampo(form, $"904_{numero}_S21_Value", mes.Horas.HasValue
                ? mes.Horas.Value.ToString() : string.Empty);
            SetCampo(form, $"905_{numero}_Text_SanSerif", mes.Notas ?? string.Empty);
        }

        int totalHoras = tarjeta.Meses.Sum(m => m.Horas ?? 0);
        int totalCursos = tarjeta.Meses.Sum(m => m.CursosBiblicos);

        SetCampo(form, "904_32_S21_Value", totalHoras > 0 ? totalHoras.ToString() : string.Empty);
        SetCampo(form, "905_32_Text_SanSerif", totalCursos > 0 ? totalCursos.ToString() : string.Empty);

        form.GetPdfObject().Put(PdfName.NeedAppearances, PdfBoolean.FALSE);
        pdfDoc.Close();
        return ms.ToArray();
    }

    private static void SetCampo(PdfAcroForm form, string nombre, string valor)
    {
        var campo = form.GetField(nombre);
        if (campo != null)
            campo.SetValue(valor);
    }

    private static void SetCheckbox(PdfAcroForm form, string nombre, bool marcado)
    {
        var campo = form.GetField(nombre);
        if (campo == null) return;
        campo.SetValue(marcado ? "Yes" : "Off");
    }

    private static string GetCampo(PdfAcroForm form, string nombre)
    {
        var campo = form.GetField(nombre);
        return campo?.GetValueAsString() ?? string.Empty;
    }

    private static bool GetCheckbox(PdfAcroForm form, string nombre)
    {
        var campo = form.GetField(nombre);
        if (campo == null) return false;
        // Solo "Yes" es verdadero — vacío o "Off" es falso
        return campo.GetValueAsString()?.Trim().ToLower() == "yes";
    }

    private static DateTime? ParseFecha(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        if (DateTime.TryParseExact(
                valor.Trim(),
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var fecha))
            return fecha;

        return null;
    }

    private static (int? horas, int cursos) LimpiarCamposPorTipo(
        TipoPublicador tipo, bool inactivo, bool participo, int? horas, int cursos)
    {
        if (inactivo || !participo) return (null, 0);
        if (tipo == TipoPublicador.Publicador || tipo == TipoPublicador.NoBautizado)
            return (null, cursos);
        return (horas, cursos);
    }

    public async Task<ApiResponse<byte[]>> DescargarTarjetaResumenAsync(int anoServicio, TipoResumenPublicador tipo)
    {
        try
        {
            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Template", "Template_Tarjeta_Publicador.pdf")))
                return ApiResponse<byte[]>.Error(
                    "Template de tarjeta no encontrado.", ErrorCatalog.ArchivoInvalido);

            // ── Nombre en el PDF ──────────────────────────────────────────────
            var nombreCongregacion = "CIPRESES";
            var nombreTipo = tipo switch
            {
                TipoResumenPublicador.Publicador => "PUBLICADORES",
                TipoResumenPublicador.PrecursorAuxiliar => "PRECURSORES AUXILIARES",
                TipoResumenPublicador.PrecursorRegular => "PRECURSORES REGULARES",
                _ => "PUBLICADORES"
            };
            var nombrePdf = $"{nombreTipo} {nombreCongregacion} {anoServicio}";

            // ── Tipos a incluir en la consulta ────────────────────────────────
            var tiposAIncluir = tipo switch
            {
                TipoResumenPublicador.Publicador => new[] { TipoPublicador.Publicador, TipoPublicador.NoBautizado },
                TipoResumenPublicador.PrecursorAuxiliar => new[] { TipoPublicador.PrecursorAuxiliar },
                TipoResumenPublicador.PrecursorRegular => new[] { TipoPublicador.PrecursorRegular },
                _ => new[] { TipoPublicador.Publicador, TipoPublicador.NoBautizado }
            };

            // ── Año de servicio ───────────────────────────────────────────────
            // anoServicio=2026 → Sep 2025 → Ago 2026
            int anoInicio = anoServicio - 1; // Sep-Dic: 2025
            int anoFin = anoServicio;     // Ene-Ago: 2026

            var mesesConfig = new[]
            {
            (IndiceForm: 20, Mes: 9,  Ano: anoInicio), // Sep 2025
            (IndiceForm: 21, Mes: 10, Ano: anoInicio), // Oct 2025
            (IndiceForm: 22, Mes: 11, Ano: anoInicio), // Nov 2025
            (IndiceForm: 23, Mes: 12, Ano: anoInicio), // Dic 2025
            (IndiceForm: 24, Mes: 1,  Ano: anoFin),    // Ene 2026
            (IndiceForm: 25, Mes: 2,  Ano: anoFin),    // Feb 2026
            (IndiceForm: 26, Mes: 3,  Ano: anoFin),    // Mar 2026
            (IndiceForm: 27, Mes: 4,  Ano: anoFin),    // Abr 2026
            (IndiceForm: 28, Mes: 5,  Ano: anoFin),    // May 2026
            (IndiceForm: 29, Mes: 6,  Ano: anoFin),    // Jun 2026
            (IndiceForm: 30, Mes: 7,  Ano: anoFin),    // Jul 2026
            (IndiceForm: 31, Mes: 8,  Ano: anoFin)     // Ago 2026
        };

            using var ms = new MemoryStream();
            using var reader = new PdfReader(
                Path.Combine(AppContext.BaseDirectory, "Template", "Template_Tarjeta_Publicador.pdf"));
            using var writer = new PdfWriter(ms);
            using var pdfDoc = new PdfDocument(reader, writer);
            var form = PdfAcroForm.GetAcroForm(pdfDoc, false);

            // ── Encabezado ────────────────────────────────────────────────────
            SetCampo(form, "900_1_Text_SanSerif", nombrePdf);
            SetCampo(form, "900_13_Text_C_SanSerif", anoServicio.ToString());

            SetCheckbox(form, "900_10_CheckBox", tipo == TipoResumenPublicador.PrecursorRegular);
            SetCheckbox(form, "900_3_CheckBox", false);
            SetCheckbox(form, "900_4_CheckBox", false);
            SetCheckbox(form, "900_6_CheckBox", false);
            SetCheckbox(form, "900_7_CheckBox", false);
            SetCheckbox(form, "900_8_CheckBox", false);
            SetCheckbox(form, "900_9_CheckBox", false);
            SetCheckbox(form, "900_11_CheckBox", false);
            SetCheckbox(form, "900_12_CheckBox", false);

            bool mostrarHoras = tipo != TipoResumenPublicador.Publicador;
            int totalCursosAnual = 0;
            int totalHorasAnual = 0;

            // ── Datos por mes ─────────────────────────────────────────────────
            foreach (var (indiceForm, mes, ano) in mesesConfig)
            {
                var informesMes = await _datInforme.GetByMesAsync(ano, mes);

                var informesFiltrados = informesMes
                    .Where(i => tiposAIncluir.Contains(i.Tipo) && !i.Inactivo)
                    .ToList();

                if (!informesFiltrados.Any()) continue;

                int cantidadParticiparon = informesFiltrados
                    .Select(i => i.IdPublicador)
                    .Distinct()
                    .Count();

                int cursosMes = informesFiltrados.Sum(i => i.CursosBiblicos);
                int horasMes = informesFiltrados.Sum(i => i.Horas ?? 0);

                totalCursosAnual += cursosMes;
                totalHorasAnual += horasMes;

                SetCheckbox(form, $"901_{indiceForm}_CheckBox", cantidadParticiparon > 0);
                SetCampo(form, $"902_{indiceForm}_Text_C_SanSerif", cursosMes > 0 ? cursosMes.ToString() : string.Empty);
                SetCampo(form, $"904_{indiceForm}_S21_Value", mostrarHoras && horasMes > 0 ? horasMes.ToString() : string.Empty);
                SetCampo(form, $"905_{indiceForm}_Text_SanSerif", cantidadParticiparon > 0 ? cantidadParticiparon.ToString() : string.Empty);
                SetCheckbox(form, $"903_{indiceForm}_CheckBox", false);
            }

            // ── Totales ───────────────────────────────────────────────────────
            SetCampo(form, "904_32_S21_Value", mostrarHoras && totalHorasAnual > 0 ? totalHorasAnual.ToString() : string.Empty);
            SetCampo(form, "905_32_Text_SanSerif", totalCursosAnual > 0 ? totalCursosAnual.ToString() : string.Empty);

            form.GetPdfObject().Put(PdfName.NeedAppearances, PdfBoolean.FALSE);
            pdfDoc.Close();
            return ApiResponse<byte[]>.Ok(ms.ToArray(), "Tarjeta resumen generada.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar tarjeta resumen: {AnoServicio} - {Tipo}.", anoServicio, tipo);
            return ApiResponse<byte[]>.Error(
                ErrorCatalog.GetMensaje(ErrorCatalog.ErrorInterno), ErrorCatalog.ErrorInterno);
        }
    }
}