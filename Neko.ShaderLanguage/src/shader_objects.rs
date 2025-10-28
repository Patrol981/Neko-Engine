pub mod shader_objects {
  pub struct ShaderStructObject {
    pub token: String,
    pub data: String,
  }

  pub struct ShaderCode {
    pub token: String,
    pub file_name_ext: String,
    pub data: String,
  }

  impl ShaderStructObject {
    pub fn empty() -> ShaderStructObject {
      ShaderStructObject {
        token: String::new(),
        data: String::new(),
      }
    }

    pub fn new(_token: String, _data: String) -> ShaderStructObject {
      ShaderStructObject {
        token: _token,
        data: _data,
      }
    }
  }

  impl ShaderCode {
    pub fn empty() -> ShaderCode {
      ShaderCode {
        token: String::new(),
        file_name_ext: String::new(),
        data: String::new(),
      }
    }

    pub fn new(_token: String, _file_name_ext: String, _data: String) -> ShaderCode {
      ShaderCode {
        token: _token,
        file_name_ext: _file_name_ext,
        data: _data,
      }
    }
  }
}
