using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class Program
{
    static string dbFile = "tareas.db";
    static string connectionString = $"Data Source={dbFile};Version=3;";

    static async Task Main()
    {
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

    static async Task CrearBaseDeDatosAsync()
    {
        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();

                string sqlTareas = "CREATE TABLE IF NOT EXISTS tareas (id INTEGER PRIMARY KEY, nombre TEXT, descripcion TEXT, tiempo INTEGER)";
                await EjecutarComandoAsync(conn, sqlTareas);

                string sqlSesiones = "CREATE TABLE IF NOT EXISTS sesiones (id INTEGER PRIMARY KEY AUTOINCREMENT, id_tarea INTEGER, fecha TEXT, duracion TEXT, FOREIGN KEY(id_tarea) REFERENCES tareas(id))";
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
        Console.Write("Ingrese el nombre de la tarea: ");
        string nombre = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(nombre) || !EsNombreValido(nombre))
        {
            Console.WriteLine("Nombre de tarea no válido. Solo se permiten letras, números y espacios.");
            Console.ReadKey();
            return;
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

                // Agregar descripción
                Console.Write("Ingrese la descripción de la tarea (opcional): ");
                string descripcion = Console.ReadLine();

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
        var tareas = new List<(int, string, string, int)>();
        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();

                string sql = "SELECT id, nombre, descripcion, tiempo FROM tareas";


                MostrarNombrePrograma();
                // Preguntar al usuario si desea ordenar
                Console.WriteLine("¿Desea ordenar las tareas? (S/N)");
                if (Console.ReadLine().Trim().ToUpper() == "S")
                {
                    Console.WriteLine("Ordenar por: 1. Nombre (ascendente) | 2. Tiempo (descendente)");
                    Console.WriteLine("Escribir 1 o 2: ");
                    if (int.TryParse(Console.ReadLine(), out int opcionOrden))
                    {
                        if (opcionOrden == 1)
                            sql += " ORDER BY nombre ASC";
                        else if (opcionOrden == 2)
                            sql += " ORDER BY tiempo DESC";
                    }
                }

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
        MostrarNombrePrograma();
        var tareas = await ObtenerTareasAsync();
        if (tareas.Count == 0)
        {
            Console.WriteLine("No hay tareas disponibles.");
        }
        else
        {
            foreach (var tarea in tareas)
            {
                Console.WriteLine($"{tarea.Item1}. {tarea.Item2} - Descripción: {tarea.Item3} - Tiempo total: {FormatoTiempo(tarea.Item4)}");
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
                            string duracion = reader.GetString(1);
                            sesiones.Add($"[{fecha}] | Sesión: {duracion}");
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

    static async Task IniciarContadorAsync(int id, string nombre, int tiempoInicial)
    {
        Console.Clear();
        MostrarNombrePrograma();
        Console.WriteLine($"Iniciando tarea: {nombre}");
        Stopwatch sw = new Stopwatch();
        sw.Start();

        bool guardadoAutomatico = false;
        Console.WriteLine("¿Desea activar el guardado automático? (S/N)");
        if (Console.ReadLine().Trim().ToUpper() == "S")
        {
            guardadoAutomatico = true;
        }

        var guardarTask = Task.Run(async () => {
            while (sw.IsRunning)
            {
                if (guardadoAutomatico)
                {
                    await GuardarTiempoAsync(id, tiempoInicial + (int)sw.Elapsed.TotalSeconds);
                }
                await Task.Delay(10000); // Guardar cada 10 segundos
            }
        });

        bool pausado = false;
        Console.WriteLine("Presione 'P' para pausar/reanudar, o cualquier otra tecla para detener...");
        Console.WriteLine("Corriendo...");
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.P)
                {
                    if (pausado)
                    {
                        sw.Start();
                        pausado = false;
                        Console.WriteLine("Corriendo...");
                    }
                    else
                    {
                        sw.Stop();
                        pausado = true;
                        Console.WriteLine("En pausa...");
                    }
                }
                else
                {
                    break;
                }
            }
            await Task.Delay(100);
        }
        sw.Stop();

        int tiempoTotal = tiempoInicial + (int)sw.Elapsed.TotalSeconds;
        string duracionSesion = FormatoTiempo((int)sw.Elapsed.TotalSeconds);
        string fechaSesion = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();

                string sqlUpdate = "UPDATE tareas SET tiempo = @tiempo WHERE id = @id";
                await EjecutarComandoAsync(conn, sqlUpdate, new SQLiteParameter("@tiempo", tiempoTotal), new SQLiteParameter("@id", id));

                string sqlInsert = "INSERT INTO sesiones (id_tarea, fecha, duracion) VALUES (@id_tarea, @fecha, @duracion)";
                await EjecutarComandoAsync(conn, sqlInsert, new SQLiteParameter("@id_tarea", id), new SQLiteParameter("@fecha", fechaSesion), new SQLiteParameter("@duracion", duracionSesion));
            }

            Console.WriteLine($"Tiempo total registrado: {FormatoTiempo(tiempoTotal)}. Presione una tecla para continuar...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al registrar la sesión: {ex.Message}");
        }

        Console.ReadKey();
    }

    static async Task GuardarTiempoAsync(int id, int tiempo)
    {
        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();
                string sqlUpdate = "UPDATE tareas SET tiempo = @tiempo WHERE id = @id";
                await EjecutarComandoAsync(conn, sqlUpdate, new SQLiteParameter("@tiempo", tiempo), new SQLiteParameter("@id", id));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al guardar el tiempo: {ex.Message}");
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