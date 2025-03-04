using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
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
            Console.Clear();
            MostrarNombrePrograma();
            Console.WriteLine();
            Console.WriteLine("Gestor de Tareas");
            Console.WriteLine("1. Agregar Tarea");
            Console.WriteLine("2. Seleccionar Tarea");
            Console.WriteLine("3. Mostrar Tareas");
            Console.WriteLine("4. Eliminar Tarea");
            Console.WriteLine("5. Salir");

            string opcion = Console.ReadLine();
            switch (opcion)
            {
                case "1":
                    await AgregarTareaAsync();
                    break;
                case "2":
                    await SeleccionarTareaAsync();
                    break;
                case "3":
                    await MostrarTareasAsync();
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

                string sqlTareas = "CREATE TABLE IF NOT EXISTS tareas (id INTEGER PRIMARY KEY, nombre TEXT, tiempo INTEGER)";
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

        if (string.IsNullOrWhiteSpace(nombre))
        {
            Console.WriteLine("El nombre de la tarea no puede estar vacío.");
            Console.ReadKey();
            return;
        }

        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();
                string sql = "INSERT INTO tareas (nombre, tiempo) VALUES (@nombre, 0)";
                await EjecutarComandoAsync(conn, sql, new SQLiteParameter("@nombre", nombre));
            }

            Console.WriteLine("Tarea agregada correctamente.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al agregar la tarea: {ex.Message}");
        }

        Console.ReadKey();
    }

    static async Task<List<(int, string, int)>> ObtenerTareasAsync()
    {
        var tareas = new List<(int, string, int)>();
        try
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                await conn.OpenAsync();
                string sql = "SELECT id, nombre, tiempo FROM tareas";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tareas.Add((reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
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
        Console.Clear();
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
                Console.WriteLine($"{tarea.Item1}. {tarea.Item2} - Tiempo total: {FormatoTiempo(tarea.Item3)}");
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
            Console.WriteLine($"{i + 1}. {tareas[i].Item2} (Tiempo total: {FormatoTiempo(tareas[i].Item3)})");
        }

        if (int.TryParse(Console.ReadLine(), out int seleccion) && seleccion > 0 && seleccion <= tareas.Count)
        {
            int idTarea = tareas[seleccion - 1].Item1;
            string nombreTarea = tareas[seleccion - 1].Item2;
            int tiempoPrevio = tareas[seleccion - 1].Item3;

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

        var updateTask = Task.Run(async () => {
            while (sw.IsRunning)
            {
                Console.SetCursorPosition(0, 2);
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, 2);
                Console.WriteLine($"Tiempo en sesión: {FormatoTiempo((int)sw.Elapsed.TotalSeconds)}");
                await Task.Delay(1000);
            }
        });

        Console.WriteLine();
        Console.WriteLine("Presione cualquier tecla para detener...");
        Console.ReadKey();
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

    static string FormatoTiempo(int segundos)
    {
        TimeSpan t = TimeSpan.FromSeconds(segundos);
        return $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
    }

    static void MostrarNombrePrograma()
    {
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
            Console.WriteLine($"{i + 1}. {tareas[i].Item2} (Tiempo: {FormatoTiempo(tareas[i].Item3)})");
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

    // Método para ejecutar comandos sin transacción
    static async Task EjecutarComandoAsync(SQLiteConnection conn, string sql, params SQLiteParameter[] parameters)
    {
        using (var cmd = new SQLiteCommand(sql, conn))
        {
            cmd.Parameters.AddRange(parameters);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // Método para ejecutar comandos con transacción
    static async Task EjecutarComandoAsync(SQLiteConnection conn, string sql, SQLiteTransaction transaction, params SQLiteParameter[] parameters)
    {
        using (var cmd = new SQLiteCommand(sql, conn, transaction))
        {
            cmd.Parameters.AddRange(parameters);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}