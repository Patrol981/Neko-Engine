pub mod shader_objects;

use std::{
  collections::HashSet,
  env,
  fs::{self, File},
  io::Write,
  path::PathBuf,
};

use shader_objects::shader_objects::{ShaderCode, ShaderStructObject};
fn main() {
  // call structure
  // dsl.exe SRC_DIR DST_DIR

  let args: Vec<String> = env::args().collect();
  let src_dir = &args[1];
  let dst_dir = &args[2];

  let cur_dir = env::current_dir().expect("Could not get current directory");

  println!("current dir -> {}", cur_dir.display());
  println!("src dir -> {}", src_dir);
  println!("dst dir -> {}", dst_dir);

  // --- LOAD IGNORE LIST ---
  let ignore_list_path = cur_dir.join(src_dir).join("ignore_list.txt");
  let ignore_list: HashSet<String> = match fs::read_to_string(&ignore_list_path) {
    Ok(contents) => contents
      .lines()
      .map(str::trim)
      .filter(|l| !l.is_empty())
      .map(String::from)
      .collect(),
    Err(_) => {
      println!(
        "Warning: could not open '{}', proceeding without ignores.",
        ignore_list_path.display()
      );
      HashSet::new()
    }
  };

  // list all structures in src directory
  let struct_dir = cur_dir.join(src_dir).join("structs");
  let struct_paths = fs::read_dir(&struct_dir)
    .unwrap_or_else(|e| panic!("Could not read structs dir {}: {}", struct_dir.display(), e));

  let mut shader_structs: Vec<ShaderStructObject> = Vec::new();
  for entry in struct_paths {
    let path = entry.unwrap().path();
    if !path.is_file() {
      continue;
    }
    shader_structs.push(ShaderStructObject::new(
      get_file_name(&path),
      read_file(&path),
    ));
  }

  // get all vertex and fragment files
  let shader_dir = cur_dir.join(src_dir);
  let shader_paths = fs::read_dir(&shader_dir)
    .unwrap_or_else(|e| panic!("Could not read shaders dir {}: {}", shader_dir.display(), e));

  let mut shader_codes: Vec<ShaderCode> = Vec::new();
  for entry in shader_paths {
    let path = entry.unwrap().path();
    if !path.is_file() {
      continue;
    }
    // check ignore list by file-stem (no extension)
    let file_stem = path.file_stem().unwrap().to_str().unwrap();
    if ignore_list.contains(file_stem) {
      println!("Ignoring shader -> {}", file_stem);
      continue;
    }
    shader_codes.push(ShaderCode::new(
      file_stem.to_string(),
      get_file_name_ext(&path),
      read_file(&path),
    ));
  }

  // process & write
  for mut sc in shader_codes {
    edit_shader_code(&mut sc, &shader_structs);
    let dst_path = cur_dir.join(dst_dir).join(&sc.file_name_ext);
    write_file(&dst_path, &sc.data);
  }
}

fn read_file(path: &PathBuf) -> String {
  let file_data = match fs::read_to_string(path) {
    Ok(result) => result,
    Err(err) => {
      panic!("[{}] {err}", path.display());
    }
  };

  return file_data;
}

fn get_file_name(path: &PathBuf) -> String {
  let file_name = path.file_stem().unwrap().to_str().unwrap();
  return file_name.to_string();
}

fn get_file_name_ext(path: &PathBuf) -> String {
  let file_name = path.file_name().unwrap().to_str().unwrap();
  return file_name.to_string();
}

fn edit_shader_code(shader_code: &mut ShaderCode, shader_structs: &Vec<ShaderStructObject>) {
  let mut modified_data = shader_code.data.clone();

  for shader_struct in shader_structs {
    let include_directive = format!("#include {}", shader_struct.token);
    if modified_data.contains(&include_directive) {
      modified_data = modified_data.replace(&include_directive, &shader_struct.data)
    }
  }

  shader_code.data = modified_data;
}

fn write_file(path: &PathBuf, data: &str) {
  let mut file = match File::create(path) {
    Ok(result) => result,
    Err(err) => {
      panic!("{err}");
    }
  };

  match file.write_all(data.as_bytes()) {
    Ok(result) => result,
    Err(err) => {
      panic!("{err}");
    }
  };
}
