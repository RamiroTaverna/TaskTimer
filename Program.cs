using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class Program
{
    static string dbFile = "tareas.db";
    static string connectionString = $"Data Source={dbFile};Version=3;";
    static int idTareaActiva = -1; // Para rastrear la tarea activa
    static int idSesionActiva = -1; // Para rastrear la sesión activa
    static Stopwatch sw = new Stopwatch(); // Cronómetro

    static async Task Main()
    {
        // Registrar el manejador de cierre
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        if (!File.Exists(dbFile))
        {
            await CrearBaseDeDatosAsync();
        }

        while (true)
        {
            MostrarNombrePrograma();
            Console.WriteLine("Gestor de Tareas");
            Console.WriteLine("1. Agregar Tarea");
            Console.WriteLine("2. Mostrar Tareas");
            Console.WriteLine("3. Seleccionar Tarea");
            Console.WriteLine("4. Eliminar Tarea");
            Console.WriteLine("5. Salir");

            string opcion = Console.ReadLine();
            switch (opcion)
            {
                case "1":
                    await AgregarTareaAsync();
                    break;
                case "2":
                    await MostrarTareasAsync();
                    break;
                case "3":
                    await SeleccionarTareaAsync();
                    break;
                case "4":
                    await EliminarTareaAsync();
                    break;
                case "5":
                    return;
                default:
                    Console.WriteLine("Opción no válida.");
                    Console.ReadKey();
                    break;
            }
        }
    }

    static void OnProcessExit(object sender, EventArgs e)
    {
        if (sw.IsRunning) // Si hay una sesión activa
        {
            sw.Stop();
            GuardarTiempoSesionAsync(idSesionActiva, idTareaActiva, (int)sw.Elapsed.TotalSeconds).Wait(); // Guardar el tiempo acumulado
            Console.WriteLine("Tiempo guardado antes de cerrar el programa.");
        }
    }

    static async Task CrearBaseDeDatosAsync()
    {
        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();

                string sqlTareas = "CREATE TABLE IF NOT EXISTS tareas (id INTEGER PRIMARY KEY, nombre TEXT, descripcion TEXT, tiempo INTEGER)";
                await EjecutarComandoAsync(conn, sqlTareas);

                string sqlSesiones = "CREATE TABLE IF NOT EXISTS sesiones (id INTEGER PRIMARY KEY AUTOINCREMENT, id_tarea INTEGER, fecha TEXT, duracion INTEGER, FOREIGN KEY(id_tarea) REFERENCES tareas(id))";
                await EjecutarComandoAsync(conn, sqlSesiones);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al crear la base de datos: {ex.Message}");
        }
    }

    static async Task AgregarTareaAsync()
    {
        Console.Clear();
        MostrarNombrePrograma();

        string nombre = "";
        string descripcion = "";

        // Validar nombre de la tarea
        while (string.IsNullOrWhiteSpace(nombre) || !EsNombreValido(nombre))
        {
            Console.Write("Ingrese el nombre de la tarea: ");
            nombre = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(nombre) || !EsNombreValido(nombre))
            {
                Console.WriteLine("Nombre de tarea no válido. Solo se permiten letras, números y espacios.");
            }
        }

        // Validar descripción de la tarea
        while (string.IsNullOrWhiteSpace(descripcion))
        {
            Console.Write("Ingrese la descripción de la tarea: ");
            descripcion = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(descripcion))
            {
                Console.WriteLine("La descripción de la tarea no puede estar vacía.");
            }
        }

        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();

                // Verificar duplicados
                string sqlVerificar = "SELECT COUNT(*) FROM tareas WHERE nombre = @nombre";
                using (var cmd = new SQLiteCommand(sqlVerificar, conn))
                {
                    cmd.Parameters.AddWithValue("@nombre", nombre);
                    int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    if (count > 0)
                    {
                        Console.WriteLine("Ya existe una tarea con ese nombre.");
                        Console.ReadKey();
                        return;
                    }
                }

                // Insertar la tarea
                string sql = "INSERT INTO tareas (nombre, descripcion, tiempo) VALUES (@nombre, @descripcion, 0)";
                await EjecutarComandoAsync(conn, sql, new SQLiteParameter("@nombre", nombre), new SQLiteParameter("@descripcion", descripcion));
            }

            Console.WriteLine("Tarea agregada correctamente.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al agregar la tarea: {ex.Message}");
        }

        Console.ReadKey();
    }

    static async Task<List<(int, string, string, int)>> ObtenerTareasAsync()
    {
        MostrarNombrePrograma();

        var tareas = new List<(int, string, string, int)>();
        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();

                // Usar COALESCE para manejar valores NULL
                string sql = "SELECT id, COALESCE(nombre, 'NULL'), COALESCE(descripcion, 'NULL'), tiempo FROM tareas";

                // Mostrar opciones de ordenamiento
                Console.WriteLine("Opciones de ordenamiento:");
                Console.WriteLine("1. Nombre (A-Z)");
                Console.WriteLine("2. Nombre (Z-A)");
                Console.WriteLine("3. Tiempo (0-9)");
                Console.WriteLine("4. Tiempo (9-0)");
                Console.WriteLine("5. Nombre (A-Z) + Tiempo (0-9)");
                Console.WriteLine("6. Nombre (A-Z) + Tiempo (9-0)");
                Console.WriteLine("7. Nombre (Z-A) + Tiempo (0-9)");
                Console.WriteLine("8. Nombre (Z-A) + Tiempo (9-0)");
                Console.WriteLine("9. ID (9-0)");
                Console.Write("Seleccione una opción (1-9): ");

                if (int.TryParse(Console.ReadLine(), out int opcionOrden))
                {
                    switch (opcionOrden)
                    {
                        case 1:
                            sql += " ORDER BY nombre ASC";
                            break;
                        case 2:
                            sql += " ORDER BY nombre DESC";
                            break;
                        case 3:
                            sql += " ORDER BY tiempo ASC";
                            break;
                        case 4:
                            sql += " ORDER BY tiempo DESC";
                            break;
                        case 5:
                            sql += " ORDER BY nombre ASC, tiempo ASC";
                            break;
                        case 6:
                            sql += " ORDER BY nombre ASC, tiempo DESC";
                            break;
                        case 7:
                            sql += " ORDER BY nombre DESC, tiempo ASC";
                            break;
                        case 8:
                            sql += " ORDER BY nombre DESC, tiempo DESC";
                            break;
                        case 9:
                            sql += " ORDER BY id DESC";
                            break;
                        default:
                            Console.WriteLine("Opción no válida. Se mostrarán las tareas sin ordenar.");
                            break;
                    }
                }
                else
                {
                    Console.WriteLine("Entrada no válida. Se mostrarán las tareas sin ordenar.");
                }

                // Espacio intencional
                Console.WriteLine();

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tareas.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener las tareas: {ex.Message}");
        }
        return tareas;
    }

    static async Task MostrarTareasAsync()
    {
        var tareas = await ObtenerTareasAsync();
        if (tareas.Count == 0)
        {
            Console.WriteLine("No hay tareas disponibles.");
        }
        else
        {
            foreach (var tarea in tareas)
            {
                Console.WriteLine($"ID: {tarea.Item1} | TAREA: {tarea.Item2} | DESCRIPCION: {tarea.Item3} | Tiempo total: {FormatoTiempo(tarea.Item4)}");
            }
        }
        Console.ReadKey();
    }

    static async Task SeleccionarTareaAsync()
    {
        var tareas = await ObtenerTareasAsync();
        if (tareas.Count == 0)
        {
            Console.Clear();
            MostrarNombrePrograma();
            Console.WriteLine("No hay tareas disponibles.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("Seleccione una tarea:");
        for (int i = 0; i < tareas.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {tareas[i].Item2} (Tiempo total: {FormatoTiempo(tareas[i].Item4)})");
        }

        if (int.TryParse(Console.ReadLine(), out int seleccion) && seleccion > 0 && seleccion <= tareas.Count)
        {
            int idTarea = tareas[seleccion - 1].Item1;
            string nombreTarea = tareas[seleccion - 1].Item2;
            int tiempoPrevio = tareas[seleccion - 1].Item4;

            Console.Clear();
            MostrarNombrePrograma();
            Console.WriteLine($"Tarea: {nombreTarea}");
            Console.WriteLine("Sesiones previas:");
            var sesiones = await ObtenerSesionesAsync(idTarea);
            if (sesiones.Count > 0)
            {
                foreach (var sesion in sesiones)
                {
                    Console.WriteLine(sesion);
                }
            }
            else
            {
                Console.WriteLine("No hay sesiones registradas.");
            }

            Console.WriteLine("\nPresione una tecla para iniciar la sesión...");
            Console.ReadKey();

            await IniciarContadorAsync(idTarea, nombreTarea, tiempoPrevio);
        }
        else
        {
            Console.WriteLine("Selección inválida.");
            Console.ReadKey();
        }
    }

    static async Task<List<string>> ObtenerSesionesAsync(int idTarea)
    {
        var sesiones = new List<string>();
        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();
                string sql = "SELECT fecha, duracion FROM sesiones WHERE id_tarea = @id_tarea ORDER BY fecha DESC";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id_tarea", idTarea);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string fecha = reader.GetString(0);
                            int duracion = reader.GetInt32(1); // Leer como entero
                            sesiones.Add($"[{fecha}] | Sesión: {FormatoTiempo(duracion)}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener las sesiones: {ex.Message}");
        }
        return sesiones;
    }

    static async Task IniciarContadorAsync(int id, string nombre, int tiempoPrevio)
    {
        Console.Clear();
        MostrarNombrePrograma();
        Console.WriteLine($"Iniciando tarea: {nombre}");
        sw.Start();
        idTareaActiva = id; // Registrar la tarea activa

        // Crear una nueva sesión con duración 0
        string fechaSesion = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        idSesionActiva = await CrearSesionAsync(id, fechaSesion);

        bool guardadoAutomatico = false;
        int intervaloGuardado = 10000; // Valor por defecto: 10 segundos
        Console.WriteLine("¿Desea activar el guardado automático? (S/N)");
        if (Console.ReadLine().Trim().ToUpper() == "S")
        {
            Console.WriteLine("Guardado automático activado. ¿Cada cuántos segundos desea guardar? (por defecto: 10)");
            if (int.TryParse(Console.ReadLine(), out int intervalo))
            {
                intervaloGuardado = intervalo * 1000; // Convertir a milisegundos
            }
            Console.WriteLine($"Guardado automático activado cada {intervaloGuardado / 1000} segundos.");
            guardadoAutomatico = true;
        }
        else
        {
            Console.WriteLine("Guardado automático desactivado. Recuerda que si se apaga el PC o cierras el programa, no se guardará el tiempo transcurrido.");
        }

        var guardarTask = Task.Run(async () =>
        {
            while (sw.IsRunning)
            {
                if (guardadoAutomatico)
                {
                    int duracion = (int)sw.Elapsed.TotalSeconds; // Obtener el tiempo transcurrido
                    await GuardarTiempoSesionAsync(idSesionActiva, idTareaActiva, duracion);
                }
                await Task.Delay(intervaloGuardado); // Guardar cada X segundos
            }
        });

        bool pausado = false;
        Console.WriteLine("Presione 'P' para pausar/reanudar, 'C' para cancelar sin guardar, o cualquier otra tecla para detener y guardar...");
        Console.WriteLine($"Corriendo... Tiempo transcurrido: {FormatoTiempo((int)sw.Elapsed.TotalSeconds)}");
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (!ManejarTecla(key, ref pausado, sw))
                {
                    break;
                }
            }
            await Task.Delay(100);
        }
        sw.Stop();

        // Guardar el tiempo final de la sesión
        int duracionSesion = (int)sw.Elapsed.TotalSeconds;
        await GuardarTiempoSesionAsync(idSesionActiva, idTareaActiva, duracionSesion);

        // Actualizar el tiempo total de la tarea
        int tiempoTotal = tiempoPrevio + duracionSesion;
        await ActualizarTiempoTareaAsync(idTareaActiva, tiempoTotal);

        Console.WriteLine($"Tiempo de sesión: {FormatoTiempo(duracionSesion)}");
        Console.WriteLine($"Tiempo total registrado: {FormatoTiempo(tiempoTotal)}. Presione una tecla para continuar...");
        Console.ReadKey();
    }

    private static bool ManejarTecla(ConsoleKey key, ref bool pausado, Stopwatch sw)
    {
        if (key == ConsoleKey.P)
        {
            if (pausado)
            {
                sw.Start();
                pausado = false;
                Console.WriteLine($"Corriendo... Tiempo transcurrido: {FormatoTiempo((int)sw.Elapsed.TotalSeconds)}");
            }
            else
            {
                sw.Stop();
                pausado = true;
                Console.WriteLine("En pausa...");
            }
            return true; // Continuar el bucle
        }
        else if (key == ConsoleKey.C)
        {
            Console.WriteLine("Sesión cancelada. No se guardará el tiempo transcurrido.");
            return false; // Salir sin guardar
        }
        else
        {
            return false; // Salir y guardar
        }
    }

    static async Task<int> CrearSesionAsync(int idTarea, string fecha)
    {
        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();
                string sql = "INSERT INTO sesiones (id_tarea, fecha, duracion) VALUES (@id_tarea, @fecha, 0); SELECT last_insert_rowid();";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id_tarea", idTarea);
                    cmd.Parameters.AddWithValue("@fecha", fecha);
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al crear la sesión: {ex.Message}");
            return -1;
        }
    }

    static async Task GuardarTiempoSesionAsync(int idSesion, int idTarea, int duracion)
    {
        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();

                // Actualizar la duración de la sesión
                string sqlUpdateSesion = "UPDATE sesiones SET duracion = @duracion WHERE id = @id";
                await EjecutarComandoAsync(conn, sqlUpdateSesion, new SQLiteParameter("@duracion", duracion), new SQLiteParameter("@id", idSesion));

                // Actualizar el tiempo total de la tarea
                string sqlUpdateTarea = "UPDATE tareas SET tiempo = tiempo + @duracion WHERE id = @id";
                await EjecutarComandoAsync(conn, sqlUpdateTarea, new SQLiteParameter("@duracion", duracion), new SQLiteParameter("@id", idTarea));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al guardar el tiempo de la sesión: {ex.Message}");
        }
    }

    static async Task ActualizarTiempoTareaAsync(int idTarea, int tiempoTotal)
    {
        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();
                string sql = "UPDATE tareas SET tiempo = @tiempo WHERE id = @id";
                await EjecutarComandoAsync(conn, sql, new SQLiteParameter("@tiempo", tiempoTotal), new SQLiteParameter("@id", idTarea));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al actualizar el tiempo de la tarea: {ex.Message}");
        }
    }

    static string FormatoTiempo(int segundos)
    {
        TimeSpan t = TimeSpan.FromSeconds(segundos);
        return $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
    }

    static void MostrarNombrePrograma()
    {
        Console.Clear();
        string nombre = "RAMA | TASKTIMER";
        Console.WriteLine(nombre);
        Console.WriteLine();
    }

    static async Task EliminarTareaAsync()
    {
        Console.Clear();
        MostrarNombrePrograma();

        var tareas = await ObtenerTareasAsync();
        if (tareas.Count == 0)
        {
            Console.WriteLine("No hay tareas para eliminar.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("Seleccione una tarea para eliminar:");
        for (int i = 0; i < tareas.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {tareas[i].Item2} (Tiempo: {FormatoTiempo(tareas[i].Item4)})");
        }

        if (int.TryParse(Console.ReadLine(), out int seleccion) && seleccion > 0 && seleccion <= tareas.Count)
        {
            int idTarea = tareas[seleccion - 1].Item1;
            string nombreTarea = tareas[seleccion - 1].Item2;

            Console.Write($"\n¿Está seguro de que desea eliminar la tarea '{nombreTarea}'? (S/N): ");
            string respuesta = Console.ReadLine().Trim().ToUpper();

            if (respuesta != "S")
            {
                Console.WriteLine("Eliminación cancelada.");
                Console.ReadKey();
                return;
            }

            try
            {
                using (var conn = new SQLiteConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            string sqlDeleteSesiones = "DELETE FROM sesiones WHERE id_tarea = @id_tarea";
                            await EjecutarComandoAsync(conn, sqlDeleteSesiones, transaction, new SQLiteParameter("@id_tarea", idTarea));

                            string sqlDeleteTarea = "DELETE FROM tareas WHERE id = @id";
                            await EjecutarComandoAsync(conn, sqlDeleteTarea, transaction, new SQLiteParameter("@id", idTarea));

                            transaction.Commit();
                            Console.WriteLine("Tarea eliminada correctamente.");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Console.WriteLine($"Error al eliminar la tarea: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al conectar con la base de datos: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Selección inválida.");
        }
        Console.ReadKey();
    }

    static async Task EjecutarComandoAsync(SQLiteConnection conn, string sql, params SQLiteParameter[] parameters)
    {
        using (var cmd = new SQLiteCommand(sql, conn))
        {
            cmd.Parameters.AddRange(parameters);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    static async Task EjecutarComandoAsync(SQLiteConnection conn, string sql, SQLiteTransaction transaction, params SQLiteParameter[] parameters)
    {
        using (var cmd = new SQLiteCommand(sql, conn, transaction))
        {
            cmd.Parameters.AddRange(parameters);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    static bool EsNombreValido(string nombre)
    {
        return Regex.IsMatch(nombre, @"^[a-zA-Z0-9\s]+$");
    }
}